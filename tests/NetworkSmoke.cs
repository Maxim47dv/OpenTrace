using System;
using System.Threading;
using System.Threading.Tasks;
using OpenTrace;
class NetworkSmoke {
 static void Main(){Run().GetAwaiter().GetResult();}
 static async Task Run(){using(Engine engine=new Engine()){
  await Task.WhenAll(Check(engine,"Telegram","telegram"),Check(engine,"TikTok","scout2015"));
 }}
 static async Task Check(Engine engine,string platform,string username){try{Target t=Target.Parse("Никнейм",username);Finding f=platform=="Telegram"?await engine.Telegram(t,CancellationToken.None):await engine.TikTok(t,CancellationToken.None);Console.WriteLine(platform+": "+f.Status+" | "+f.Title);}catch(Exception e){Console.WriteLine(platform+": network unavailable ("+e.GetType().Name+")");}}
}
