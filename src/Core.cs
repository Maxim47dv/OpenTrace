using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using System.IO;

namespace OpenTrace {
public class Target {
 public string Kind,Value;
 public static Target Parse(string kind,string raw){
  string s=(raw??"").Trim();if(s.Length==0||s.Length>254)throw new ArgumentException("Введите номер, почту или никнейм.");
  if(kind=="Телефон"){if(!Regex.IsMatch(s,@"^\+[0-9 ()\-\.]+$"))throw new ArgumentException("Используйте международный формат: +код страны и номер.");s="+"+Regex.Replace(s,@"\D","");if(!Regex.IsMatch(s,@"^\+[1-9][0-9]{6,14}$"))throw new ArgumentException("Проверьте длину номера: от 7 до 15 цифр.");}
  else if(kind=="Почта"){MailAddress a;try{a=new MailAddress(s);}catch{throw new ArgumentException("Проверьте адрес почты.");}if(a.Address!=s||s.Contains(" ")||!a.Host.Contains("."))throw new ArgumentException("Введите только адрес почты, без имени.");s=a.User+"@"+new IdnMapping().GetAscii(a.Host).ToLowerInvariant();}
  else if(kind=="Никнейм"){s=s.TrimStart('@');if(!Regex.IsMatch(s,@"^[A-Za-z0-9_\.\-]{2,64}$"))throw new ArgumentException("Введите никнейм без ссылки: буквы, цифры, _, точка или дефис.");}
  else throw new ArgumentException("Неизвестный тип запроса.");
  return new Target{Kind=kind,Value=s};
 }
}
public class Finding {
 public string Platform{get;set;} public string Title{get;set;} public string Status{get;set;}
 public string Url{get;set;} public string Evidence{get;set;} public string Source{get;set;}
 public string CheckedUtc{get;set;} public string Assessment{get;set;} public string Note{get;set;}
 public Finding(){CheckedUtc=DateTime.UtcNow.ToString("o");Assessment="Не оценено";Note="";}
}
public class Fetch {public int Code;public string Text;}
public class Engine : IDisposable {
 readonly HttpClient client; public Func<string,CancellationToken,Task<Fetch>> Transport;
 public Engine(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});client.Timeout=TimeSpan.FromSeconds(20);Transport=Get;}
 async Task<Fetch> Get(string url,CancellationToken token){using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(20));token=deadline.Token;using(HttpRequestMessage req=new HttpRequestMessage(HttpMethod.Get,url)){req.Headers.TryAddWithoutValidation("User-Agent","OpenTrace/0.2 (public-source research)");req.Headers.TryAddWithoutValidation("Accept","application/rss+xml,application/xml,application/json,text/html");using(HttpResponseMessage res=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,token)){if(res.Content.Headers.ContentLength>2097152)throw new Exception("Ответ слишком большой.");using(var stream=await res.Content.ReadAsStreamAsync()){using(var ms=new System.IO.MemoryStream()){byte[] buffer=new byte[8192];int n;while((n=await stream.ReadAsync(buffer,0,buffer.Length,token))>0){if(ms.Length+n>2097152)throw new Exception("Ответ слишком большой.");ms.Write(buffer,0,n);}return new Fetch{Code=(int)res.StatusCode,Text=System.Text.Encoding.UTF8.GetString(ms.ToArray())};}}}}}}
 public static string Clean(string html){return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html??"","<[^>]+>"," ")),@"\s+"," ").Trim();}
 public static string Platform(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo!="")return "";string h=u.DnsSafeHost.ToLowerInvariant();foreach(string pair in new[]{"t.me|Telegram","telegram.me|Telegram","tiktok.com|TikTok","instagram.com|Instagram","vk.com|ВКонтакте","facebook.com|Facebook","x.com|X","twitter.com|X","github.com|GitHub","reddit.com|Reddit","youtube.com|YouTube","wa.me|WhatsApp"}){string[] p=pair.Split('|');if(h==p[0]||h.EndsWith("."+p[0]))return p[1];}return "";}
 public static string Query(Target t){return "\""+t.Value+"\" (site:t.me OR site:tiktok.com OR site:instagram.com OR site:vk.com OR site:facebook.com OR site:x.com OR site:github.com OR site:reddit.com OR site:youtube.com OR site:wa.me)";}
 public static bool Matches(Target target,string title,string evidence,string url){
  string content=Clean(title+" "+evidence)+" "+Uri.UnescapeDataString(url);
  if(target.Kind=="Телефон"){
   string digits=target.Value.Substring(1);var pattern=new System.Text.StringBuilder(@"(?<![0-9])\+?");
   for(int i=0;i<digits.Length;i++){if(i>0)pattern.Append(@"[\s().-]{0,5}");pattern.Append(digits[i]);}pattern.Append(@"(?![0-9])");return Regex.IsMatch(content,pattern.ToString());
  }
  string boundary=target.Kind=="Почта"?@"A-Za-z0-9_.@+\-":@"A-Za-z0-9_";
  return Regex.IsMatch(content,"(?<!["+boundary+"])"+Regex.Escape(target.Value)+"(?!["+boundary+"])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
 }
 public static List<Finding> ParseRss(string xml,Target target){
  XmlDocument doc=new XmlDocument();doc.XmlResolver=null;
  try{using(XmlReader reader=XmlReader.Create(new StringReader(xml??""),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152})){doc.Load(reader);}}
  catch(XmlException){throw new Exception("Поисковик не вернул RSS-выдачу. Возможно ограничение доступа; повторите позже.");}
  if(doc.DocumentElement==null||doc.DocumentElement.Name!="rss"||doc.SelectSingleNode("/rss/channel")==null)throw new Exception("Неожиданный ответ поискового источника. Наличие аккаунтов не установлено.");
  var result=new List<Finding>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(XmlNode item in doc.SelectNodes("/rss/channel/item")){
   XmlNode link=item.SelectSingleNode("link");string url=link==null?"":link.InnerText.Trim();string platform=Platform(url);if(platform=="")continue;
   XmlNode heading=item.SelectSingleNode("title"),description=item.SelectSingleNode("description");string title=Clean(heading==null?"":heading.InnerText),evidence=Clean(description==null?"":description.InnerText);
   if(!Matches(target,title,evidence,url)||!seen.Add(url))continue;
   result.Add(new Finding{Platform=platform,Title=title,Status="Публичное упоминание",Url=url,Evidence=evidence,Source="Bing — публичная RSS-выдача"});
  }
  return result;
 }
 public async Task<List<Finding>> Search(Target target,CancellationToken token){
  token.ThrowIfCancellationRequested();string url="https://www.bing.com/search?format=rss&q="+Uri.EscapeDataString(Query(target));Fetch response=await Transport(url,token);token.ThrowIfCancellationRequested();
  if(response.Code==403||response.Code==429)throw new Exception("Поисковик временно ограничил доступ. Повторите позже; это не означает отсутствие аккаунтов.");
  if(response.Code!=200)throw new Exception("Поисковый источник недоступен: HTTP "+response.Code);
  return ParseRss(response.Text,target);
 }
 public static string Field(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key]!=null?Convert.ToString(d[key]):"";}
 public async Task<Finding> TikTok(Target t,CancellationToken token){string page="https://www.tiktok.com/@"+Uri.EscapeDataString(t.Value);string api="https://www.tiktok.com/oembed?url="+Uri.EscapeDataString(page);Fetch response=await Transport(api,token);Finding f=new Finding{Platform="TikTok",Url=page,Title="@"+t.Value,Source="TikTok oEmbed",Status="Не удалось проверить",Evidence="Сервис не подтвердил публичную страницу. Это не доказывает отсутствие аккаунта."};if(response.Code==200){var d=new JavaScriptSerializer().DeserializeObject(response.Text) as Dictionary<string,object>;if(d!=null){string author=Field(d,"author_url");if(string.Equals(author.TrimEnd('/'),page,StringComparison.OrdinalIgnoreCase)){f.Status="Публичная страница";f.Title=Clean(Field(d,"author_name"));f.Evidence="TikTok oEmbed вернул публичную страницу с этим никнеймом. Связь с другими аккаунтами и личность владельца не подтверждены.";}}}return f;}
 public async Task<Finding> Telegram(Target t,CancellationToken token){string page="https://t.me/"+Uri.EscapeDataString(t.Value);Fetch response=await Transport(page,token);Finding f=new Finding{Platform="Telegram",Title="@"+t.Value,Url=page,Source="Публичная страница t.me",Status="Не удалось проверить",Evidence="Публичная страница не подтверждена. Telegram может ограничивать доступ; отсутствие аккаунта не установлено."};if(response.Code==200){Match m=Regex.Match(response.Text,@"<div\b[^>]*class=[""'][^""']*\btgme_page_title\b[^""']*[""'][^>]*>(.*?)</div>",RegexOptions.Singleline|RegexOptions.IgnoreCase);if(m.Success&&response.Text.Contains("tgme_page_photo_image")){f.Title=Clean(m.Groups[1].Value);f.Status="Публичная страница";f.Evidence="На t.me есть публичная карточка с заголовком «"+f.Title+"». Это может быть пользователь, бот, группа или канал. Совпадение никнейма не устанавливает личность.";}}return f;}
 public void Dispose(){client.Dispose();}
}
}
