using System;
using System.Threading;
using System.Threading.Tasks;
using OpenTrace;
class Tests {
 static int n;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
 static void Main(){try{Run().GetAwaiter().GetResult();Console.WriteLine(n+" checks passed.");}catch(Exception e){Console.WriteLine(e);Environment.ExitCode=1;}}
 static async Task Run(){
  Target email=Target.Parse("Почта","test@example.com"),phone=Target.Parse("Телефон","+1 (202) 555-0100"),user=Target.Parse("Никнейм","example_user");
  Check(phone.Value=="+12025550100","phone normalization");bool invalid=false;try{Target.Parse("Телефон","2025550100");}catch(ArgumentException){invalid=true;}Check(invalid,"country code required");
  Check(Engine.Platform("https://t.me.evil.example/a")=="","spoofed domain rejected");Check(Engine.Platform("https://t.me@evil.example/a")=="","URL credentials rejected");Check(Engine.Platform("javascript:alert(1)")=="","non-HTTPS link rejected");
  Check(Engine.Matches(email,"","Contact test@example.com","https://t.me/example"),"email in public snippet matched");Check(!Engine.Matches(email,"","other-test@example.com","https://t.me/example"),"longer email cannot match");Check(Engine.Matches(phone,"","+1 (202) 555-0100","https://t.me/example"),"formatted phone matched");Check(!Engine.Matches(phone,"","9120255501009","https://t.me/example"),"longer number cannot match");Check(Engine.Matches(user,"","","https://www.tiktok.com/@example_user"),"username URL matched");Check(!Engine.Matches(user,"Other","","https://t.me/other"),"unrelated username rejected");
  string sample="<rss><channel><item><title>Example</title><link>https://t.me/example</link><description>test@example.com &amp; public</description></item><item><link>https://t.me/example</link><description>test@example.com</description></item><item><link>https://t.me.evil.example/a</link><description>test@example.com</description></item><item><link>https://github.com/unrelated</link><description>other contact</description></item></channel></rss>";
  var findings=Engine.ParseRss(sample,email);Check(findings.Count==1,"RSS filtered and deduplicated");Check(findings[0].Evidence=="test@example.com & public","RSS entities decoded");Check(findings[0].Assessment=="Не оценено","public mention is not proof of identity");Check(Engine.ParseRss("<rss><channel/></rss>",email).Count==0,"valid empty feed");
  bool bad=false;try{Engine.ParseRss("<html>challenge</html>",email);}catch(Exception){bad=true;}Check(bad,"HTML challenge not treated as empty results");
  bool dtd=false;try{Engine.ParseRss("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///not-a-real-file'>]><rss><channel>&x;</channel></rss>",email);}catch(Exception){dtd=true;}Check(dtd,"external XML entities prohibited");
  using(Engine engine=new Engine()){
   string sent="";engine.Transport=delegate(string url,CancellationToken token){sent=url;return Task.FromResult(new Fetch{Code=200,Text=sample});};Check((await engine.Search(email,CancellationToken.None)).Count==1&&sent.StartsWith("https://www.bing.com/search?format=rss&q="),"automatic search without a key");Check(Uri.UnescapeDataString(sent).Contains("\"test@example.com\""),"query input quoted");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=429,Text=""});};bool limited=false;try{await engine.Search(email,CancellationToken.None);}catch(Exception e){limited=e.Message.Contains("ограничил");}Check(limited,"rate limit surfaced");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=302,Text=""});};bool redirect=false;try{await engine.Search(email,CancellationToken.None);}catch(Exception e){redirect=e.Message.Contains("302");}Check(redirect,"redirect is inconclusive");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=403,Text=""});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Не удалось проверить","TikTok block is inconclusive");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="{\"author_url\":\"https://www.tiktok.com/@other\"}"});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Не удалось проверить","wrong TikTok author rejected");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="{\"author_url\":\"https://www.tiktok.com/@example_user\",\"author_name\":\"Example\"}"});};Check((await engine.TikTok(user,CancellationToken.None)).Status=="Публичная страница","TikTok public page recognized");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="<html>Contact landing</html>"});};Check((await engine.Telegram(user,CancellationToken.None)).Status=="Не удалось проверить","generic Telegram page is inconclusive");
   engine.Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="<div class=\"tgme_page_title\">Example Channel</div><img class=\"tgme_page_photo_image\">"});};Check((await engine.Telegram(user,CancellationToken.None)).Title=="Example Channel","Telegram public title extracted");
   using(var cts=new CancellationTokenSource()){cts.Cancel();bool canceled=false;try{await engine.Search(email,cts.Token);}catch(OperationCanceledException){canceled=true;}Check(canceled,"cancellation prevents query");}
  }
 }
}
