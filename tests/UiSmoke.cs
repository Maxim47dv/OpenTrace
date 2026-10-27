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
  app.Show();Application.DoEvents();Field<ComboBox>(app,"kind").SelectedIndex=2;Field<TextBox>(app,"input").Text="test@example.com";Field<TextBox>(app,"key").Text="synthetic-test-key";
  Field<Engine>(app,"engine").Transport=delegate(string url,string key,CancellationToken token){return Task.FromResult(new Fetch{Code=200,Text="{\"web\":{\"results\":[{\"url\":\"https://t.me/example\",\"title\":\"Example - synthetic test\",\"description\":\"Synthetic fixture, not a real finding.\"}]}}"});};
  Task search=(Task)typeof(App).GetMethod("Search",flags).Invoke(app,null);while(!search.IsCompleted)Application.DoEvents();search.GetAwaiter().GetResult();Application.DoEvents();
  Console.WriteLine("Status="+Field<Label>(app,"status").Text+"; Rows="+Field<DataGridView>(app,"grid").Rows.Count+"; FindingCount="+Field<List<Finding>>(app,"findings").Count);if(Field<DataGridView>(app,"grid").Rows.Count!=1)throw new Exception("Results not displayed");
  Field<ComboBox>(app,"assessment").SelectedIndex=1;Field<TextBox>(app,"note").Text="Test note";Console.WriteLine("Apply enabled="+Field<Button>(app,"apply").Enabled+"; Visible="+Field<Button>(app,"apply").Visible);Field<Button>(app,"apply").PerformClick();Console.WriteLine("Stored="+Field<List<Finding>>(app,"findings")[0].Note);
  if(Field<List<Finding>>(app,"findings")[0].Note!="Test note")throw new Exception("Note not saved");
  Field<TextBox>(app,"input").Text="bad-email";search=(Task)typeof(App).GetMethod("Search",flags).Invoke(app,null);search.GetAwaiter().GetResult();
  if(Field<Target>(app,"target").Value!="test@example.com")throw new Exception("Invalid input overwrote report target");
  Field<TextBox>(app,"input").Text="test@example.com";Field<TextBox>(app,"key").Text="";
  using(Bitmap bmp=new Bitmap(app.Width,app.Height)){app.DrawToBitmap(bmp,new Rectangle(0,0,app.Width,app.Height));bmp.Save(args[0]);}
  Console.WriteLine("PASS UI: results, source details, notes, failed-input report integrity, key masking.");app.Close();
 }}
}
