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
 readonly HttpClient client; public Func<string,string,CancellationToken,Task<Fetch>> Transport;
 public Engine(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate});client.Timeout=TimeSpan.FromSeconds(20);Transport=Get;}
 async Task<Fetch> Get(string url,string key,CancellationToken token){using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(20));token=deadline.Token;using(HttpRequestMessage req=new HttpRequestMessage(HttpMethod.Get,url)){req.Headers.TryAddWithoutValidation("User-Agent","OpenTrace/0.1 (public-source research)");req.Headers.TryAddWithoutValidation("Accept","application/json,text/html");if(!string.IsNullOrEmpty(key))req.Headers.TryAddWithoutValidation("X-Subscription-Token",key);using(HttpResponseMessage res=await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,token)){if(res.Content.Headers.ContentLength>2097152)throw new Exception("Ответ слишком большой.");using(var stream=await res.Content.ReadAsStreamAsync()){using(var ms=new System.IO.MemoryStream()){byte[] buffer=new byte[8192];int n;while((n=await stream.ReadAsync(buffer,0,buffer.Length,token))>0){if(ms.Length+n>2097152)throw new Exception("Ответ слишком большой.");ms.Write(buffer,0,n);}return new Fetch{Code=(int)res.StatusCode,Text=System.Text.Encoding.UTF8.GetString(ms.ToArray())};}}}}}}
 public static string Clean(string html){return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html??"","<[^>]+>"," ")),@"\s+"," ").Trim();}
 public static string Platform(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||u.Scheme!="https"||u.UserInfo!="")return "";string h=u.DnsSafeHost.ToLowerInvariant();foreach(string pair in new[]{"t.me|Telegram","telegram.me|Telegram","tiktok.com|TikTok","instagram.com|Instagram","vk.com|ВКонтакте","facebook.com|Facebook","x.com|X","twitter.com|X","github.com|GitHub","reddit.com|Reddit","youtube.com|YouTube","wa.me|WhatsApp"}){string[] p=pair.Split('|');if(h==p[0]||h.EndsWith("."+p[0]))return p[1];}return "";}
 public static string Query(Target t){return "\""+t.Value+"\" (site:t.me OR site:tiktok.com OR site:instagram.com OR site:vk.com OR site:facebook.com OR site:x.com OR site:github.com OR site:reddit.com OR site:youtube.com OR site:wa.me)";}
 public static List<Finding> ParseSearch(string json){var root=new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;if(root==null||!root.ContainsKey("web"))return new List<Finding>();var web=root["web"] as Dictionary<string,object>;if(web==null||!web.ContainsKey("results"))return new List<Finding>();var results=web["results"] as object[];if(results==null)throw new Exception("Неожиданный формат поискового ответа.");var found=new List<Finding>();var seen=new HashSet<string>();foreach(object item in results){var d=item as Dictionary<string,object>;if(d==null)continue;string url=Field(d,"url"),platform=Platform(url);if(platform==""||!seen.Add(url))continue;found.Add(new Finding{Platform=platform,Title=Clean(Field(d,"title")),Status="Кандидат из поиска",Url=url,Evidence=Clean(Field(d,"description")),Source="Brave Search API"});}return found;}
 public static string Field(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key]!=null?Convert.ToString(d[key]):"";}
 public async Task<List<Finding>> Search(Target t,string key,CancellationToken token){if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("Для автоматического поиска нужен ключ Brave Search API. Введите его в поле выше. Ключ хранится только до закрытия окна.");string url="https://api.search.brave.com/res/v1/web/search?q="+Uri.EscapeDataString(Query(t))+"&count=20&extra_snippets=false";Fetch response=await Transport(url,key.Trim(),token);if(response.Code==401||response.Code==403)throw new Exception("Поисковый API отклонил ключ или доступ к тарифу.");if(response.Code==429)throw new Exception("Достигнут лимит поискового API. Повторите позже.");if(response.Code!=200)throw new Exception("Поисковый API: HTTP "+response.Code);return ParseSearch(response.Text);}
 public async Task<Finding> TikTok(Target t,CancellationToken token){string page="https://www.tiktok.com/@"+Uri.EscapeDataString(t.Value);string api="https://www.tiktok.com/oembed?url="+Uri.EscapeDataString(page);Fetch response=await Transport(api,"",token);Finding f=new Finding{Platform="TikTok",Url=page,Title="@"+t.Value,Source="TikTok oEmbed",Status="Не удалось проверить",Evidence="Сервис не подтвердил публичную страницу. Это не доказывает отсутствие аккаунта."};if(response.Code==200){var d=new JavaScriptSerializer().DeserializeObject(response.Text) as Dictionary<string,object>;if(d!=null){string author=Field(d,"author_url");if(string.Equals(author.TrimEnd('/'),page,StringComparison.OrdinalIgnoreCase)){f.Status="Публичная страница";f.Title=Clean(Field(d,"author_name"));f.Evidence="TikTok oEmbed вернул публичную страницу с этим никнеймом. Связь с другими аккаунтами и личность владельца не подтверждены.";}}}return f;}
 public async Task<Finding> Telegram(Target t,CancellationToken token){string page="https://t.me/"+Uri.EscapeDataString(t.Value);Fetch response=await Transport(page,"",token);Finding f=new Finding{Platform="Telegram",Title="@"+t.Value,Url=page,Source="Публичная страница t.me",Status="Не удалось проверить",Evidence="Публичная страница не подтверждена. Telegram может ограничивать доступ; отсутствие аккаунта не установлено."};if(response.Code==200){Match m=Regex.Match(response.Text,@"<div\b[^>]*class=[""'][^""']*\btgme_page_title\b[^""']*[""'][^>]*>(.*?)</div>",RegexOptions.Singleline|RegexOptions.IgnoreCase);if(m.Success&&response.Text.Contains("tgme_page_photo_image")){f.Title=Clean(m.Groups[1].Value);f.Status="Публичная страница";f.Evidence="На t.me есть публичная карточка с заголовком «"+f.Title+"». Это может быть пользователь, бот, группа или канал. Совпадение никнейма не устанавливает личность.";}}return f;}
 public void Dispose(){client.Dispose();}
}
}
