using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using OpenTrace;
class Tests {
 static int count;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static void Reject(Action action,string label){try{action();}catch(ArgumentException){Check(true,label);return;}throw new Exception(label);}
 static void Main(){Run().GetAwaiter().GetResult();Console.WriteLine(count+" checks passed.");}
 static async Task Run(){
  Check(Target.Parse("Телефон","+1 (202) 555-0100").Value=="+12025550100","international phone normalization");
  Reject(delegate{Target.Parse("Телефон","2025550100");},"country code is required");
  Reject(delegate{Target.Parse("Телефон","+1<script>");},"phone rejects injected text");
  Reject(delegate{Target.Parse("Почта","Name <a@example.com>");},"display names are rejected");
  Check(Target.Parse("Почта","a@EXAMPLE.COM").Value=="a@example.com","email domain normalized");
  Check(Target.Parse("Никнейм","@example_user").Value=="example_user","username normalization");
  Reject(delegate{Target.Parse("Никнейм","https://t.me/example");},"username rejects URLs");
  Check(Engine.Platform("https://t.me.evil.example/person")=="","spoofed domain rejected");
  Check(Engine.Platform("https://t.me@evil.example/person")=="","URL credentials rejected");
  Check(Engine.Platform("javascript:alert(1)")=="","non-HTTPS link rejected");
  Check(Engine.Platform("https://www.tiktok.com/@example")=="TikTok","real TikTok URL recognized");
  string sample="{\"web\":{\"results\":[{\"url\":\"https://t.me/example\",\"title\":\"<b>Example</b>\",\"description\":\"Public &amp; indexed\"},{\"url\":\"https://t.me/example\",\"title\":\"Duplicate\"},{\"url\":\"https://t.me.evil.example/a\",\"title\":\"Spoof\"}]}}";
  var findings=Engine.ParseSearch(sample);Check(findings.Count==1,"search results filtered and deduplicated");Check(findings[0].Title=="Example"&&findings[0].Evidence=="Public & indexed","snippets converted to plain text");Check(findings[0].Status=="Кандидат из поиска"&&findings[0].Assessment=="Не оценено","search is never treated as proof of identity");Check(Engine.ParseSearch("{\"web\":{\"results\":[]}}").Count==0,"empty search result");
  using(Engine engine=new Engine()){
   Target email=Target.Parse("Почта","test@example.com");string sentUrl="",sentKey="";
   engine.Transport=delegate(string url,string key,CancellationToken token){sentUrl=url;sentKey=key;return Task.FromResult(new Fetch{Code=200,Text=sample});};
   var result=await engine.Search(email,"test-secret",CancellationToken.None);Check(result.Count==1&&sentKey=="test-secret"&&!sentUrl.Contains("test-secret"),"API key passed separately from query URL");
   Check(Uri.UnescapeDataString(sentUrl).Contains("\"test@example.com\""),"email sent as quoted exact query");
   bool noKey=false;try{await engine.Search(email,"",CancellationToken.None);}catch(ArgumentException){noKey=true;}Check(noKey,"missing API key is explicit");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=429,Text=""});};bool limit=false;try{await engine.Search(email,"test",CancellationToken.None);}catch(Exception e){limit=e.Message.Contains("лимит");}Check(limit,"rate limit surfaced");
   Target user=Target.Parse("Никнейм","example_user");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=403,Text="blocked"});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Не удалось проверить","TikTok block is inconclusive");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="{\"author_url\":\"https://www.tiktok.com/@other\",\"author_name\":\"Other\"}"});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Не удалось проверить","wrong TikTok author cannot confirm requested profile");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="{\"author_url\":\"https://www.tiktok.com/@example_user\",\"author_name\":\"Example\"}"});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Публичная страница","matching TikTok oEmbed confirms page only");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="<html>Telegram contact landing</html>"});};Check((await engine.Telegram(user,CancellationToken.None)).Status=="Не удалось проверить","generic Telegram landing is inconclusive");
   engine.Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="<div class=\"tgme_page_title\"><span>Example Channel</span></div><img class=\"tgme_page_photo_image\">"});};Check((await engine.Telegram(user,CancellationToken.None)).Title=="Example Channel","public Telegram title parsed");
   engine.Transport=delegate(string url,string key,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new Fetch{Code=200,Text=sample});};bool canceled=false;using(var cts=new CancellationTokenSource()){cts.Cancel();try{await engine.Search(email,"test",cts.Token);}catch(OperationCanceledException){canceled=true;}}Check(canceled,"cancellation propagated");
   string report=new JavaScriptSerializer().Serialize(new {Findings=findings});Check(!report.Contains("test-secret")&&report.Contains("CheckedUtc"),"report excludes credentials and retains timestamps");
  }
 }
}
