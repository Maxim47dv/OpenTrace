using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTrace;
class UiSmoke {
 static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,flags).GetValue(obj);}
 [STAThread]static void Main(string[] args){try{Run(args);}catch(Exception e){Console.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){Application.EnableVisualStyles();using(App app=new App()){
  app.Show();Application.DoEvents();Field<ComboBox>(app,"kind").SelectedIndex=2;Field<TextBox>(app,"input").Text="test@example.com";
  Field<Engine>(app,"engine").Transport=delegate(string url,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="<rss><channel><item><title>Example - synthetic test</title><link>https://t.me/example</link><description>Synthetic test@example.com fixture, not a real finding.</description></item></channel></rss>"});};
  foreach(Control c in app.Controls)if(c.Text.Contains("API key")||c.Text.Contains("Brave")||c.Text.Contains("Поиск в браузере"))throw new Exception("Legacy key UI remains");
  Task search=(Task)typeof(App).GetMethod("Search",flags).Invoke(app,null);while(!search.IsCompleted)Application.DoEvents();search.GetAwaiter().GetResult();Application.DoEvents();
  if(Field<DataGridView>(app,"grid").Rows.Count!=1)throw new Exception("Results not displayed without a key");
  Field<ComboBox>(app,"assessment").SelectedIndex=1;Field<TextBox>(app,"note").Text="Test note";Console.WriteLine("Apply enabled="+Field<Button>(app,"apply").Enabled+"; Visible="+Field<Button>(app,"apply").Visible);Field<Button>(app,"apply").PerformClick();Console.WriteLine("Stored="+Field<List<Finding>>(app,"findings")[0].Note);
  if(Field<List<Finding>>(app,"findings")[0].Note!="Test note")throw new Exception("Note not saved");
  Field<TextBox>(app,"input").Text="bad-email";search=(Task)typeof(App).GetMethod("Search",flags).Invoke(app,null);search.GetAwaiter().GetResult();
  if(Field<Target>(app,"target").Value!="test@example.com")throw new Exception("Invalid input overwrote report target");
  Field<TextBox>(app,"input").Text="test@example.com";
  using(Bitmap bmp=new Bitmap(app.Width,app.Height)){app.DrawToBitmap(bmp,new Rectangle(0,0,app.Width,app.Height));bmp.Save(args[0]);}
  Console.WriteLine("PASS UI: results, source details, notes, failed-input report integrity, no API key controls.");app.Close();
 }}
}
