using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace OpenTrace {
class App : Form {
 readonly Color bg=Color.FromArgb(15,21,33),panel=Color.FromArgb(25,34,49),ink=Color.FromArgb(231,238,249),muted=Color.FromArgb(158,176,199),accent=Color.FromArgb(105,224,190);
 readonly Engine engine=new Engine();TextBox input,key,details,note;ComboBox kind,assessment;DataGridView grid;Label status;Button run,stop,export,open,apply;ProgressBar progress;CancellationTokenSource cancellation;List<Finding> findings=new List<Finding>();Target target;bool busy;
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new App());}
 public App(){Text="OpenTrace — публичные цифровые следы";ClientSize=new Size(1150,800);MinimumSize=MaximumSize=Size;MaximizeBox=false;BackColor=bg;ForeColor=ink;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;
  LabelAt("OpenTrace",26,18,300,45,new Font("Segoe UI",25,FontStyle.Bold),ink);LabelAt("ПУБЛИЧНЫЕ ИСТОЧНИКИ  /  0.1",738,35,385,25,new Font("Segoe UI",9),accent);
  LabelAt("Номер, почта или никнейм → ссылки, доказательства и ваша оценка",28,70,1090,27,Font,muted);
  kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};kind.Items.AddRange(new object[]{"Никнейм","Телефон","Почта"});kind.SelectedIndex=0;kind.SetBounds(28,119,150,32);Controls.Add(kind);
  input=Input(190,119,640,32,false);run=Btn("Найти",846,116,134,37,async delegate{await Search();});stop=Btn("Стоп",994,116,128,37,delegate{if(cancellation!=null)cancellation.Cancel();});stop.Enabled=false;
  LabelAt("Brave API key",28,169,150,26,Font,muted);key=Input(190,167,640,29,true);Btn("Получить ключ ↗",846,164,276,35,delegate{Visit("https://api-dashboard.search.brave.com/");});
  LabelAt("Ключ хранится в памяти. При поиске запрос отправляется Brave; для ника также проверяются t.me и TikTok.",28,207,1090,26,new Font("Segoe UI",9),muted);
  Btn("Поиск в браузере ↗",28,250,215,36,delegate{Manual();});export=Btn("Экспорт JSON",257,250,180,36,delegate{Export();});export.Enabled=false;
  LabelAt("Результат поиска — кандидат, а не доказанная принадлежность аккаунта человеку.",457,257,665,29,new Font("Segoe UI",9),accent);
  progress=new ProgressBar{Style=ProgressBarStyle.Marquee,Visible=false};progress.SetBounds(28,300,1094,5);Controls.Add(progress);
  grid=new DataGridView{ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,BackgroundColor=panel,BorderStyle=BorderStyle.None,RowHeadersVisible=false,EnableHeadersVisualStyles=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};grid.SetBounds(28,319,1094,229);grid.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;grid.DefaultCellStyle.BackColor=panel;grid.DefaultCellStyle.ForeColor=ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(42,70,84);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(32,44,63);grid.ColumnHeadersDefaultCellStyle.ForeColor=muted;grid.ColumnHeadersHeight=35;grid.RowTemplate.Height=36;grid.GridColor=Color.FromArgb(42,53,72);
  Column("Сервис","Platform",16);Column("Страница / заголовок","Title",34);Column("Статус источника","Status",27);Column("Ваша оценка","Assessment",23);grid.SelectionChanged+=delegate{ShowSelected();};Controls.Add(grid);
  LabelAt("ИСТОЧНИК И ДОКАЗАТЕЛЬСТВО",28,564,650,24,new Font("Segoe UI",9,FontStyle.Bold),muted);details=Input(28,593,690,137,false);details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;
  assessment=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};assessment.Items.AddRange(new object[]{"Не оценено","Возможное совпадение","Связь подтверждена мной","Другой человек"});assessment.SelectedIndex=0;assessment.SetBounds(738,591,384,30);Controls.Add(assessment);note=Input(738,630,384,45,false);note.Multiline=true;
  apply=Btn("Сохранить оценку",738,688,188,37,delegate{Finding f=Selected();if(f==null)return;f.Assessment=Convert.ToString(assessment.SelectedItem);f.Note=note.Text;RefreshGrid(f);});open=Btn("Источник ↗",940,688,182,37,delegate{Finding f=Selected();if(f!=null)Visit(f.Url);});open.Enabled=apply.Enabled=false;
  status=LabelAt("Готово. Введите данные. Автопоиск по телефону и почте требует ключ Brave Search API.",28,755,1094,35,new Font("Segoe UI",9),muted);AcceptButton=run;
 }
 Label LabelAt(string s,int x,int y,int w,int h,Font font,Color color){Label l=new Label{Text=s,Font=font,ForeColor=color};l.SetBounds(x,y,w,h);Controls.Add(l);return l;}
 TextBox Input(int x,int y,int w,int h,bool secret){TextBox t=new TextBox{BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,UseSystemPasswordChar=secret};t.SetBounds(x,y,w,h);Controls.Add(t);return t;}
 Button Btn(string text,int x,int y,int w,int h,EventHandler action){Button b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=ink,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(57,77,99);b.SetBounds(x,y,w,h);b.Click+=action;Controls.Add(b);return b;}
 void Column(string title,string property,int width){grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,DataPropertyName=property,FillWeight=width,SortMode=DataGridViewColumnSortMode.NotSortable});}
 Finding Selected(){return grid.CurrentRow==null?null:grid.CurrentRow.DataBoundItem as Finding;}
 void ShowSelected(){Finding f=Selected();if(open==null||apply==null||details==null||note==null||assessment==null)return;open.Enabled=apply.Enabled=f!=null;if(f==null){details.Clear();note.Clear();return;}details.Text=f.Source+" • "+f.CheckedUtc+Environment.NewLine+f.Url+Environment.NewLine+Environment.NewLine+f.Evidence;assessment.SelectedItem=f.Assessment;note.Text=f.Note;}
 void RefreshGrid(Finding select){grid.DataSource=null;grid.DataSource=new System.ComponentModel.BindingList<Finding>(findings);if(select==null&&findings.Count>0)select=findings[0];if(select!=null){int i=findings.IndexOf(select);if(i>=0&&i<grid.Rows.Count)grid.CurrentCell=grid.Rows[i].Cells[0];}export.Enabled=findings.Count>0;ShowSelected();}
 async Task AddModule(string platform,Func<Task<Finding>> work,CancellationToken token){try{Finding f=await work();token.ThrowIfCancellationRequested();findings.Add(f);RefreshGrid(f);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;findings.Add(new Finding{Platform=platform,Title="Тайм-аут",Status="Ошибка проверки",Evidence="Источник не ответил вовремя; наличие аккаунта не установлено.",Url=platform=="Telegram"?"https://t.me/":"https://www.tiktok.com/",Source=platform});RefreshGrid(null);}catch(Exception){findings.Add(new Finding{Platform=platform,Title="Источник недоступен",Status="Ошибка проверки",Evidence="Не удалось прочитать ответ источника. Повторите позже; это не доказывает отсутствие аккаунта.",Url=platform=="Telegram"?"https://t.me/":"https://www.tiktok.com/",Source=platform});RefreshGrid(null);}}
 async Task Search(){if(busy)return;try{Target pendingTarget=Target.Parse(Convert.ToString(kind.SelectedItem),input.Text);if(pendingTarget.Kind!="Никнейм"&&string.IsNullOrWhiteSpace(key.Text))throw new ArgumentException("Для автоматического поиска по телефону или почте введите ключ Brave Search API. Без ключа доступен поиск в браузере.");target=pendingTarget;}catch(Exception e){status.Text=e.Message;return;}
  findings.Clear();RefreshGrid(null);busy=true;run.Enabled=kind.Enabled=input.Enabled=key.Enabled=false;stop.Enabled=true;progress.Visible=true;cancellation=new CancellationTokenSource();CancellationToken token=cancellation.Token;List<string> notices=new List<string>();status.Text="Идёт поиск публичных страниц…";
  try{if(!string.IsNullOrWhiteSpace(key.Text)){try{findings.AddRange(await engine.Search(target,key.Text,token));RefreshGrid(null);}catch(OperationCanceledException){throw;}catch(Exception e){notices.Add(e.Message);}}
   if(target.Kind=="Никнейм"){if(System.Text.RegularExpressions.Regex.IsMatch(target.Value,@"^[A-Za-z][A-Za-z0-9_]{3,31}$"))await AddModule("Telegram",delegate{return engine.Telegram(target,token);},token);if(System.Text.RegularExpressions.Regex.IsMatch(target.Value,@"^[A-Za-z0-9_\.]{2,24}$"))await AddModule("TikTok",delegate{return engine.TikTok(target,token);},token);if(string.IsNullOrWhiteSpace(key.Text))notices.Add("Без ключа проверены только публичные страницы Telegram и TikTok.");}
   status.Text="Готово: "+findings.Count+" записей. "+(notices.Count>0?string.Join(" ",notices.ToArray()):"Принадлежность аккаунтов человеку требует проверки.");
  }catch(OperationCanceledException){status.Text=token.IsCancellationRequested?"Поиск остановлен. Полученные записи сохранены в таблице.":"Источник не ответил вовремя. Повторите позже.";}catch(Exception e){status.Text="Ошибка поиска: "+e.Message;}
  finally{busy=false;run.Enabled=kind.Enabled=input.Enabled=key.Enabled=true;stop.Enabled=false;progress.Visible=false;cancellation.Dispose();cancellation=null;export.Enabled=findings.Count>0;}
 }
 void Manual(){try{Target t=Target.Parse(Convert.ToString(kind.SelectedItem),input.Text);Visit("https://search.brave.com/search?q="+Uri.EscapeDataString(Engine.Query(t)));}catch(Exception e){status.Text=e.Message;}}
 void Visit(string url){Uri u;if(!Uri.TryCreate(url,UriKind.Absolute,out u)||u.Scheme!="https"){status.Text="Небезопасная или отсутствующая ссылка.";return;}try{Process.Start(new ProcessStartInfo(u.AbsoluteUri){UseShellExecute=true});}catch(Exception e){status.Text="Не удалось открыть браузер: "+e.Message;}}
 void Export(){if(target==null||findings.Count==0)return;using(SaveFileDialog dialog=new SaveFileDialog{Filter="JSON report|*.json",FileName="opentrace-report-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".json"})if(dialog.ShowDialog()==DialogResult.OK){try{var report=new{Application="OpenTrace 0.1",CreatedUtc=DateTime.UtcNow.ToString("o"),Target=target,Notice="Публичные страницы и поисковые кандидаты. Принадлежность человеку не установлена автоматически.",Findings=findings};File.WriteAllText(dialog.FileName,new JavaScriptSerializer().Serialize(report),new System.Text.UTF8Encoding(false));status.Text="Отчёт сохранён.";}catch(Exception e){status.Text="Не удалось сохранить отчёт: "+e.Message;}}}
 protected override void OnFormClosing(FormClosingEventArgs e){if(cancellation!=null)cancellation.Cancel();base.OnFormClosing(e);}
 protected override void Dispose(bool disposing){if(disposing)engine.Dispose();base.Dispose(disposing);}
}
}
