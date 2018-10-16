using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;

namespace JSONSerializer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Root Root;
        private Root RootSplitting;

        private List<JSON_FSSs> ListForSearchFSS = new List<JSON_FSSs>();
        private List<JSON_site> ListForSearchSites = new List<JSON_site>();

        private OpenFileDialog OpenFileDlg;
        private OpenFileDialog OpenSplittingFileDlg;
        private DispatcherTimer Timer = new DispatcherTimer();

        private bool flag = true;
        private bool isFocused;

        public MainWindow()
        {
            InitializeComponent();

            Timer.Interval = TimeSpan.FromSeconds(3);
            Timer.Tick += OnTick;

            HideOrShowElements(Visibility.Hidden);

            OpenFileDlg = new OpenFileDialog // создаем объект и устанавливаем начальную директорию, а также запрет на выбор нескольких файлов.
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                Multiselect = false
            };

            OpenSplittingFileDlg = new OpenFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                Multiselect = false
            };

            OpenSplittingFile_Button.Content = " Выбрать файл\nдля соединения";
        }

        // скрыть все кроме кнопки выбора файла, а позже и показать все остальное.
        private void HideOrShowElements(Visibility active)
        {
            IEnumerable<UIElement> elements = MainWindowGrid.Children.Cast<UIElement>().Where(x => !((x as Button) != null && (x as Button).Name == "OpenFileButton"));

            foreach (var item in elements)
            {
                item.Visibility = active;
            }
        }

        private void RewriteRootData(ref Root root, OpenFileDialog fileDlg)
        {
            /*  13.07 добавил проверку и на строку "Ни каких доменов не найдено", потому что 
                программма указывала все равно на один и тот же объект. Метод ReferencesEqual был заюзан.
                Видимо для экномии места ссылки ссылаются на одно и тоже место.
            */
            List<JSON_site> listSites = root.Sites.Where(x =>
                x.Domains.Except(new string[] { "", "Ни каких доменов не найдено" }).Count() == 0 ||
                (x.Phrase_search != null &&
                (x.Phrase_search.Interval == null ||
                x.Phrase_search.Must_include == null ||
                x.Phrase_search.Must_include_one_of == null ||
                x.Phrase_search.Must_not_include == null)) ||
                x.Validation_interval == null ||
                x.FSS_search_interval == null).ToList();

            List<JSON_FSSs> listFSS = root.FSSs.Where(x => 
            x.Domains.Except(new string[] { "", "Ни каких доменов не найдено" }).Count() == 0 || 
            x.Validation == null).ToList();

            foreach (var item in listFSS)
            {
                if (item.Domains.Except(new string[] { "", "Ни каких доменов не найдено" }).Count() == 0)
                    item.Domains = new string[] { "Ни каких доменов не найдено " + GetElementId(item, Root.FSSs) };

                if (item.Validation == null)
                    item.Validation = new Validation();
            }

            //int indexInSites;
            foreach (var item in listSites)
            {
                try
                {
                    if (item.Domains.Except(new string[] { "", "Ни каких доменов не найдено" }).Count() == 0)
                        item.Domains = new string[] { "Ни каких доменов не найдено " + GetElementId(item, Root.Sites) };

                    if (item.Phrase_search != null &&
                        item.Phrase_search.Interval == null &&
                        item.Phrase_search.Must_include == null &&
                        item.Phrase_search.Must_include_one_of == null &&
                        item.Phrase_search.Must_not_include == null) { item.Phrase_search = null; }

                    if (item.Phrase_search != null)
                    {
                        if (item.Phrase_search.Interval == null)
                            item.Phrase_search.Interval = new Interval();

                        if (item.Phrase_search.Must_include == null)
                            item.Phrase_search.Must_include = new List<string>().ToArray();

                        if (item.Phrase_search.Must_include_one_of == null)
                            item.Phrase_search.Must_include_one_of = new List<string>().ToArray();

                        if (item.Phrase_search.Must_not_include == null)
                            item.Phrase_search.Must_not_include = new List<string>().ToArray();
                    }

                    if (item.FSS_search_interval == null)
                        item.FSS_search_interval = new Interval();

                    if (item.Validation_interval == null)
                        item.Validation_interval = new Interval();
                }
                catch (Exception ex)
                {
                    ShowException(ex, "Domains = " + string.Join("", item.Domains));
                }
            }

            WriteOnFile(x => { }, fileDlg, root);
        }

        private string CheckEncoding(OpenFileDialog fDlg)
        {
            string str = File.ReadAllText(fDlg.FileName, Encoding.UTF8);

            if (str.Contains("�"))
            {
                str = File.ReadAllText(fDlg.FileName, Encoding.Default);
            }

            return str;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
            OpenFileDlg.ShowDialog(); // вызываем диалоговое окно

            if (OpenFileDlg.FileName != "")
            {
                if (File.Exists(OpenFileDlg.FileName))
                {
                    try
                    {
                        string check = CheckEncoding(OpenFileDlg);

                        Root = JsonConvert.DeserializeObject<Root>(check, new JsonSerializerSettings() { Formatting = Formatting.Indented });

                        RewriteRootData(ref Root, OpenFileDlg);

                        Sites_RadioButton.IsChecked = true;
                        Switch_FSS_Sites();

                        InfoLabel.Content = "Файл " + System.IO.Path.GetFileName(OpenFileDlg.FileName) + " открыт";

                        HideOrShowElements(Visibility.Visible);
                        Delete_Button.Visibility = Visibility.Hidden;
                        Saved.Visibility = Visibility.Hidden;
                    }
                    catch (Exception ex)
                    {
                        HideOrShowElements(Visibility.Hidden);
                        ShowException(ex);
                        MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    HideOrShowElements(Visibility.Hidden);
                    MessageBox.Show("Указанный Вами файл не существует", "Ошибка открытия файла, \n нажмите на кнопку \"Открыть файл\" еще раз и повторите попытку.");
                    return;
                }
            }
            else
            {
                HideOrShowElements(Visibility.Hidden);
                MessageBox.Show("Вы не указали файл", "Ошибка открытия файла, \n нажмите на кнопку \" Открыть файл\" еще раз и повторите попытку.");
                return;
            }

            InfoSplittingFileLabel.Visibility = Visibility.Hidden;
        }

        private void ClearAll()
        {
            DomainsList.Text = "";
            Must_Include.Text = "";
            Must_Include_One_Of.Text = "";
            Must_Not_Include.Text = "";

            FSS_IsEnabled.IsChecked = false;
            FSS_Search_Interval_From.Text = "";
            FSS_Search_Interval_To.Text = "";

            Phrase_Search_Interval_From.Text = "";
            Phrase_Search_Interval_To.Text = "";

            Validation_Interval_From.Text = "";
            Validation_Interval_To.Text = "";
        }

        private int GetGlobalIndexFromString(string item)
        {
            item = item.Replace(item.Substring(0, item.IndexOf(" - ") + 3), "");
            return Int32.Parse(item.Substring(0, item.IndexOf(" - "))) - 1;
        }

        private void SetCurrent(int id)
        {
            ClearAll();
            if ((bool)Sites_RadioButton.IsChecked)
            {
                if (Root.Sites[id].FSS_search_enabled)
                {
                    FSS_IsEnabled.IsChecked = true;
                    if (Root.Sites[id].FSS_search_interval != null)
                    {
                        FSS_Search_Interval_From.Text = Root.Sites[id].FSS_search_interval.From;
                        FSS_Search_Interval_To.Text = Root.Sites[id].FSS_search_interval.To;
                    }

                }
                else
                    FSS_IsEnabled.IsChecked = false;

                if (Root.Sites[id].Domains.Count() > 0)
                {
                    DomainsList.Text = string.Join("\n", Root.Sites[id].Domains);
                }

                if (Root.Sites[id].Phrase_search != null)
                {
                    if (Root.Sites[id].Phrase_search.Interval != null)
                    {
                        Phrase_Search_Interval_From.Text = Root.Sites[id].Phrase_search.Interval.From;
                        Phrase_Search_Interval_To.Text = Root.Sites[id].Phrase_search.Interval.To;
                    }

                    if (Root.Sites[id].Phrase_search.Must_include != null)
                    {
                        Must_Include.Text = string.Join("\n", Root.Sites[id].Phrase_search.Must_include);
                    }

                    if (Root.Sites[id].Phrase_search.Must_include_one_of != null)
                    {
                        Must_Include_One_Of.Text = string.Join("\n", Root.Sites[id].Phrase_search.Must_include_one_of);
                    }

                    if (Root.Sites[id].Phrase_search.Must_not_include != null)
                    {
                        Must_Not_Include.Text = string.Join("\n", Root.Sites[id].Phrase_search.Must_not_include);
                    }
                }

                if (Root.Sites[id].Validation_interval != null)
                {
                    Validation_Interval_From.Text = Root.Sites[id].Validation_interval.From;
                    Validation_Interval_To.Text = Root.Sites[id].Validation_interval.To;
                }
            }
            else
            {
                if (Root.FSSs[id].Domains.Count() > 0)
                {
                    DomainsList.Text = string.Join("\n", Root.FSSs[id].Domains);
                }

                if (Root.FSSs[id].Validation.Must_include != null)
                {
                    Must_Include.Text = string.Join("\n", Root.FSSs[id].Validation.Must_include);
                }

                if (Root.FSSs[id].Validation.Must_include_one_of != null)
                {
                    Must_Include_One_Of.Text = string.Join("\n", Root.FSSs[id].Validation.Must_include_one_of);
                }

                if (Root.FSSs[id].Validation.Must_not_include != null)
                {
                    Must_Not_Include.Text = string.Join("\n", Root.FSSs[id].Validation.Must_not_include);
                }
            }
        }

        /*
         * 27.07 8:44 - Добавлено исключение vk.com  
         */
        private string OnlyDomain(string url)
        {
            url = url.StartsWith("http://") ? url.Replace("http://", "") : url;
            url = url.StartsWith("https://") ? url.Replace("https://", "") : url;
            url = url.StartsWith("www.") ? url.Replace("www.", "") : url;

            if (!url.StartsWith("vk.com"))
            {
                if (url.Contains('/'))
                    url = url.Substring(0, url.IndexOf('/'));
            }

            return url;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ListForSearchFSS.Clear();
            ListForSearchSites.Clear();
            FoundedItemsList.Items.Clear();
            //ClearAll();

            TextBoxSearch_ID.Text = "";

            if (!string.IsNullOrEmpty(TextBoxSearch_Domain.Text))
            {
                ClearAll();
                TextBoxSearch_Domain.Text = OnlyDomain(TextBoxSearch_Domain.Text);
                // выбрать только те, у которых в массиве строк Domains есть строки, содержащие TextBoxSearch.Text

                if ((bool)Sites_RadioButton.IsChecked)
                {
                    ListForSearchSites.AddRange(Root.Sites.Where(x => x.Domains.Where(z => z.ToLower().Contains(TextBoxSearch_Domain.Text.ToLower())).Count() > 0));
                    if (ListForSearchSites.Count == 0)
                    {
                        FoundedItemsList.Items.Clear();
                        ResultSearchLabel.Content = "Не найдено элементов";
                    }
                    else
                    {
                        ResultSearchLabel.Content = "Найдено элементов: " + ListForSearchSites.Count;
                        ShowALLElements(ListForSearchSites);
                        ListForSearchSites.Clear();
                    }
                }
                else
                {
                    ListForSearchFSS.AddRange(Root.FSSs.Where(x => x.Domains.Where(z => z.ToLower().Contains(TextBoxSearch_Domain.Text.ToLower())).Count() > 0));
                    if (ListForSearchFSS.Count == 0)
                    {
                        FoundedItemsList.Items.Clear();
                        ResultSearchLabel.Content = "Не найдено элементов";
                    }
                    else
                    {
                        ResultSearchLabel.Content = "Найдено элементов: " + ListForSearchFSS.Count;
                        ShowALLElements(ListForSearchFSS);
                        ListForSearchFSS.Clear();
                    }
                }

                FoundedItemsList.SelectedIndex = -1;
            }
            else
            {
                ResultSearchLabel.Content = "Найдено элементов: ";

                if ((bool)Sites_RadioButton.IsChecked)
                    ShowALLElements(Root.Sites);
                else
                    ShowALLElements(Root.FSSs);
            }
        }

        /*
         * 13.07
         * Убрал некторые строки в методе GetElementId, были излишни. 
         */

        private int GetElementId(JSON_site site, List<JSON_site> sites)
        {
            if (site == null) return -1;

            return sites.FindIndex(x => x.Equals(x, site)) + 1;
        }

        private int GetElementId(JSON_FSSs site, List<JSON_FSSs> sites)
        {
            if (site == null) return -1;

            return sites.FindIndex(x => x.Equals(x, site)) + 1; 
        }

        private void ShowALLElements(List<JSON_FSSs> list)
        {
            FoundedItemsList.Items.Clear();

            foreach (var item in list)
            {
                FoundedItemsList.Items.Add(GetElementId(item, list) + " item - " + GetElementId(item, Root.FSSs) + " - " + string.Join(", ", item.Domains) + " ");
            }
        }

        private void ShowALLElements(List<JSON_site> list)
        {
            FoundedItemsList.Items.Clear();

            foreach (var item in list)
            {
                FoundedItemsList.Items.Add(GetElementId(item, list) + " item - " + GetElementId(item, Root.Sites) + " - " + string.Join(", ", item.Domains) + " ");
            }
        }

        private void TextBoxSearch_ID_TextChanged(object sender, TextChangedEventArgs e)
        {
            ListForSearchSites.Clear();
            ListForSearchFSS.Clear();
            ClearAll();
            TextBoxSearch_Domain.Text = "";

            if (!string.IsNullOrEmpty(TextBoxSearch_ID.Text))
            {
                try
                {
                    int id = Int32.Parse(TextBoxSearch_ID.Text) - 1;

                    if ((bool)Sites_RadioButton.IsChecked)
                    {
                        List<JSON_site> list = Root.Sites;

                        if (id > list.Count - 1 || id < 0)
                        {
                            FoundedItemsList.Items.Clear();
                            ResultSearchLabel.Content = "Не найдено элементов";
                        }
                        else
                        {
                            FoundedItemsList.Items.Clear();
                            FoundedItemsList.Items.Add("1 item - " + (id + 1) + " - " + string.Join(", ", list[id].Domains));
                            ResultSearchLabel.Content = "Найдено элементов: 1";
                        }
                    }
                    else
                    {
                        List<JSON_FSSs> list = Root.FSSs;

                        if (id > list.Count - 1 || id < 0)
                        {
                            FoundedItemsList.Items.Clear();
                            ResultSearchLabel.Content = "Не найдено элементов";
                        }
                        else
                        {
                            FoundedItemsList.Items.Clear();
                            FoundedItemsList.Items.Add("1 item - " + (id + 1) + " - " + string.Join(", ", list[id].Domains));
                            ResultSearchLabel.Content = "Найдено элементов: 1";
                        }
                    }

                    ResultSearchLabel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    ResultSearchLabel.Content = "Не найдено элементов";
                    ShowException(ex);
                }
            }
            else
            {
                ResultSearchLabel.Content = "Найдено элементов: ";

                if ((bool)Sites_RadioButton.IsChecked)
                    ShowALLElements(Root.Sites);
                else
                    ShowALLElements(Root.FSSs);
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            Saved.Visibility = Visibility.Hidden;
            Timer.Stop();
        }

        /*
        27.07 8:08 - добавлена возможность сохранять домены с неуказанными интервалами поиска FSS и Validation_interval, 
        при этом будет выскакивать окно подтверждения

        25.08 11:46 - Добавлена возможность сохранять домены с неуказанными интервалами поиска фразы, 
        при этом будет выскакивать окно подтверждения
        */

        private bool IsValid(bool FSSOrSites)
        {
            string action = "Сохранение";
            switch (FSSOrSites)
            {
                case true:

                    if ((!string.IsNullOrEmpty(Phrase_Search_Interval_From.Text) || !string.IsNullOrEmpty(Phrase_Search_Interval_To.Text)) &&
                        (string.IsNullOrEmpty(Must_Include.Text) && string.IsNullOrEmpty(Must_Include_One_Of.Text) && string.IsNullOrEmpty(Must_Not_Include.Text)))
                    {
                        MessageBox.Show("Один из интервалов Phrase_search - указан, и ни одного поля из типа Must." +
                            "\n" + action + " не будет осуществлено", "Ошибка при проверке Phrase_search");
                        return false;
                    }

                    if ((string.IsNullOrEmpty(Phrase_Search_Interval_From.Text) || string.IsNullOrEmpty(Phrase_Search_Interval_To.Text)) &&
                        (!string.IsNullOrEmpty(Must_Include.Text) || !string.IsNullOrEmpty(Must_Include_One_Of.Text) || !string.IsNullOrEmpty(Must_Not_Include.Text)))
                    {
                        if (MessageBox.Show("Один из интервалов Phrase_search - не указан, а одно из полей типа Must - указано." +
                            "\nВсе равно сохранить?", "Подтверждение при проверке Phrase_search", MessageBoxButton.YesNo) == MessageBoxResult.No)
                            return false;
                    }

                    if (!(bool)FSS_IsEnabled.IsChecked && (!string.IsNullOrEmpty(FSS_Search_Interval_From.Text) || !string.IsNullOrEmpty(FSS_Search_Interval_To.Text)))
                    {
                        MessageBox.Show("FSS - отключен, но интервалы FSS зачем-то указаны.\n" + action + " не будет осуществлено", "Ошибка");
                        return false;
                    }

                    if ((bool)FSS_IsEnabled.IsChecked && (string.IsNullOrEmpty(FSS_Search_Interval_From.Text) || string.IsNullOrEmpty(FSS_Search_Interval_To.Text)))
                    {
                        if (MessageBox.Show("FSS - включен, но один из интервалов FSS отсутствует.\nВсе равно сохранить?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.No)
                            return false;
                    }

                    if (string.IsNullOrEmpty(Validation_Interval_From.Text) || string.IsNullOrEmpty(Validation_Interval_To.Text))
                    {
                        if (MessageBox.Show("Один из интервалов Validation_interval не указан.\nВсе равно сохранить?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.No)
                            return false;
                    }

                    break;



                case false:

                    if (string.IsNullOrEmpty(Must_Include.Text) && string.IsNullOrEmpty(Must_Include_One_Of.Text) && string.IsNullOrEmpty(Must_Not_Include.Text))
                    {
                        MessageBox.Show("Все поля из типа Must пустые.\n" + action + " не будет осуществлено", "Ошибка при проверке Validation");
                        return false;
                    }

                    break;
            }

            if (string.IsNullOrEmpty(DomainsList.Text))
            {
                MessageBox.Show("Домены остутствуют.\n" + action + " не будет осуществлено", "Ошибка при проверке доменов");
                return false;
            }

            return true;
        }

        private Phrase_search AddingPhrase()
        {
            if (!string.IsNullOrEmpty(Phrase_Search_Interval_From.Text) && !string.IsNullOrEmpty(Phrase_Search_Interval_To.Text) &&
                (!string.IsNullOrEmpty(Must_Include.Text) || !string.IsNullOrEmpty(Must_Include_One_Of.Text) || !string.IsNullOrEmpty(Must_Not_Include.Text)))
            {
                return new Phrase_search
                {
                    Must_include = Must_Include.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    Must_include_one_of = Must_Include_One_Of.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    Must_not_include = Must_Not_Include.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                    Interval = new Interval
                    {
                        From = Phrase_Search_Interval_From.Text,
                        To = Phrase_Search_Interval_To.Text
                    }
                };
            }
            return null;
        } // общая часть которая будет передавать в делегат.

        /*
         Добавление и удаление немного отличаются, 
         делегат все рашает, передаем именно ту часть кода, 
         которая нужна в конкретном случае (удаление или добавление). 
         */

        private delegate void ActionWithJSONObject(Phrase_search addingPhrase);

        /*
         * практически весь код одинаковый, 
         * только код в делегате отличается, 
         * поэтому все кроме кода делегата 
         * кидаем в общий метод - WriteOnFile.
        */

        private void WriteOnFile(ActionWithJSONObject actWithObj, OpenFileDialog OpFileDlg, Root root)
        {
            actWithObj(AddingPhrase());

            try
            {
                // убираем нулевую записи о поиске фраз, если есть.
                string set = ",\r\n      \"phrase_search\": null";
                File.WriteAllText(OpFileDlg.FileName, JsonConvert.SerializeObject(root, Formatting.Indented).Replace(set, ""), Encoding.UTF8);

                //File.WriteAllText(OpFileDlg.FileName, JsonConvert.SerializeObject(root, Formatting.Indented).Contains("\"phrase_search\": null") ?
                //JsonConvert.SerializeObject(root, Formatting.Indented).Replace(set, "") : JsonConvert.SerializeObject(root, Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        // метод для раставления табуляци в выходной строке, пока не нужен.
        private StringBuilder StringOutput(string inputString)
        {
            //StringBuilder str = new StringBuilder(inputString);
            //str = str.Replace("[", " [").Replace(":{", ": {");

            //str.Replace(",", "\r\n" + new string(' ', inputString.));

            //--------------------------------------

            StringBuilder copy = new StringBuilder(inputString).Replace("[", " [").Replace(":{", ": {");

            int indexCopy = 0;
            string currStr = "";
            string space = "";

            string[] b = new string[5] { "{", "}", "[", "]", "," };
            int[] a = new int[5];
            string temp2;

            for (int i = 0; i < copy.Length; i += a.Min() + 1)
            {
                char check = copy[i];
                if (copy[i] == '{' ||
                    copy[i] == '[' && copy[i + 1] != ']' ||
                    copy[i] == '}' && copy[i + 1] != ',' ||
                    copy[i] == ',' ||
                    copy[i] == ']' && copy[i + 1] != ',' ||
                    copy[i] == '"' && copy[i + 1] == ']')
                {
                    currStr = copy.ToString(0, copy.Length - (copy.Length - i) + 1);
                    indexCopy = currStr.Where(x => x == '{' || x == '[').Count() - currStr.Where(x => x == ']' || x == '}').Count();

                    if (copy[i] == ']' && copy[i + 1] != ',' ||
                        copy[i] == '"' && copy[i + 1] == ']' ||
                        copy[i] == '}' && copy[i + 1] != ',')
                        indexCopy--;

                    space = "\r\n" + new string(' ', indexCopy * 2);
                    copy = copy.Insert(i + 1, space);
                    i += space.Length;
                }

                temp2 = copy.ToString(i + 1, copy.Length - i - 1);
                for (int j = 0; j < a.Length; j++)
                {
                    a[j] = temp2.IndexOf(b[j]);
                }

                Window.Title = "" + i;
            }
            return copy;
        }

        private void ReloadData()
        {
            ClearAll();

            if ((bool)Sites_RadioButton.IsChecked)
            {
                CountOfAllElements.Content = "Всего элементов найдено в файле: " + Root.Sites.Count;
                ShowALLElements(Root.Sites);
            }
            else
            {
                CountOfAllElements.Content = "Всего элементов найдено в файле: " + Root.FSSs.Count;
                ShowALLElements(Root.FSSs);
            }
            Saved.Visibility = Visibility.Visible;
            Timer.Start();
        }

        // очищаем форму для ввода будущих данных.
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(DomainsList.Text) ||
                !string.IsNullOrEmpty(FSS_Search_Interval_From.Text) ||
                !string.IsNullOrEmpty(FSS_Search_Interval_To.Text) ||
                !string.IsNullOrEmpty(Validation_Interval_From.Text) ||
                !string.IsNullOrEmpty(Validation_Interval_To.Text) ||
                !string.IsNullOrEmpty(Phrase_Search_Interval_From.Text) ||
                !string.IsNullOrEmpty(Phrase_Search_Interval_To.Text) ||
                !string.IsNullOrEmpty(Must_Include.Text) ||
                !string.IsNullOrEmpty(Must_Include_One_Of.Text) ||
                !string.IsNullOrEmpty(Must_Not_Include.Text))
            {
                if (FoundedItemsList.SelectedIndex < 0 && MessageBox.Show("Поля будут очищены, продолжить?", "Проверка", MessageBoxButton.YesNo) == MessageBoxResult.No)
                {
                    return;
                }
            }

            FoundedItemsList.SelectedIndex = -1;
            Delete_Button.Visibility = Visibility.Hidden;
            ClearAll();
        }

        // читай про добавление.
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!IsValid((bool)Sites_RadioButton.IsChecked))
                return;

            WriteOnFile(x =>
            {
                if ((bool)Sites_RadioButton.IsChecked)
                {
                    JSON_site newSite = new JSON_site
                    {
                        Domains = DomainsList.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                        FSS_search_enabled = (bool)FSS_IsEnabled.IsChecked,
                        FSS_search_interval = new Interval
                        {
                            From = FSS_Search_Interval_From.Text,
                            To = FSS_Search_Interval_To.Text
                        },
                        Phrase_search = x,
                        Validation_interval = new Interval
                        {
                            From = Validation_Interval_From.Text,
                            To = Validation_Interval_To.Text
                        }
                    };

                    if (FoundedItemsList.SelectedIndex > -1)
                    {
                        int id = GetGlobalIndexFromString(FoundedItemsList.SelectedValue.ToString());
                        if (id > -1 && id < Root.Sites.Count)
                        {
                            Root.Sites[id] = newSite;
                        }
                    }
                    else
                    {
                        Root.Sites.Add(newSite);
                    }
                }
                else
                {
                    JSON_FSSs newSite = new JSON_FSSs
                    {
                        Domains = DomainsList.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                        Validation = new Validation
                        {
                            Must_include = Must_Include.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                            Must_include_one_of = Must_Include_One_Of.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                            Must_not_include = Must_Not_Include.Text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        }
                    };

                    if (FoundedItemsList.SelectedIndex > -1)
                    {
                        int id = GetGlobalIndexFromString(FoundedItemsList.SelectedValue.ToString());
                        if (id > -1 && id < Root.Sites.Count)
                            Root.FSSs[id] = newSite;
                    }
                    else
                        Root.FSSs.Add(newSite);
                }
            }, OpenFileDlg, Root);
            ReloadData();
        }

        // ввод только чисел.
        private void TextBoxSearch_ID_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!Char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private void FoundedItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoundedItemsList.SelectedIndex > -1)
            {
                Delete_Button.Visibility = Visibility.Visible;
                SetCurrent(GetGlobalIndexFromString(FoundedItemsList.SelectedValue.ToString()));
            }
            else
                Delete_Button.Visibility = Visibility.Hidden;
        }

        private void Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите удалить элемент?", "Подтверждение удаления", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                WriteOnFile(x =>
                {
                    int id = GetGlobalIndexFromString(FoundedItemsList.SelectedValue.ToString());
                    if (id > -1)
                    {
                        if ((bool)Sites_RadioButton.IsChecked)
                        {
                            if (id < Root.Sites.Count)
                            {
                                Root.Sites.RemoveAt(id);
                            }
                        }
                        else
                        {
                            if (id < Root.FSSs.Count)
                            {
                                Root.FSSs.RemoveAt(id);
                            }
                        }
                    }
                }, OpenFileDlg, Root);
                ReloadData();
                FoundedItemsList.SelectedIndex = -1;
            }
        }

        private void OpenSplittingFile_Click(object sender, RoutedEventArgs e)
        {
            OpenSplittingFileDlg.ShowDialog(); // вызываем диалоговое окно

            if (OpenSplittingFileDlg.FileName != "")
            {
                if (File.Exists(OpenSplittingFileDlg.FileName))
                {
                    try
                    {
                        RootSplitting = JsonConvert.DeserializeObject<JSONSerializer.Root>(CheckEncoding(OpenSplittingFileDlg)); // открываем файл, и читаем объекты.

                        RewriteRootData(ref RootSplitting, OpenSplittingFileDlg);

                        InfoSplittingFileLabel.Visibility = Visibility.Visible;
                        InfoSplittingFileLabel.Content = "Файл " + System.IO.Path.GetFileName(OpenSplittingFileDlg.FileName) + " открыт для соединения";
                    }
                    catch (Exception ex)
                    {
                        ShowException(ex);
                    }
                }
                else
                {
                    MessageBox.Show("Указанный Вами файл не существует", "Ошибка открытия файла для соединения, \n нажмите на кнопку \" Открыть файл для соединения\" еще раз и повторите попытку.");
                    return;
                }
            }
            else
            {
                MessageBox.Show("Вы не указали файл", "Ошибка открытия файла для соединения, \n нажмите на кнопку \" Открыть файл для соединения\" еще раз и повторите попытку.");
                InfoSplittingFileLabel.Visibility = Visibility.Hidden;
                return;
            }
        }

        private List<string> DifferenceBetweenObjects(JSON_FSSs mainObj, JSON_FSSs splittingObj)
        {
            string separator = new string('*', 20);

            List<string> report = new List<string>
                {
                    "Рассматриваются item с доменами: ",
                    "В главном файле:  " + string.Join(", ", mainObj.Domains),
                    "В соединяемом файле:  " + string.Join(", ", splittingObj.Domains),
                    separator
                };

            if (!mainObj.Validation.Equals(splittingObj.Validation))
            {
                report.Add("Параметры Validation различаются: ");
                if (!mainObj.Validation.Must_include.Equals(splittingObj.Validation.Must_include))
                {
                    report.Add("  Параметр Must_Include в основном item: " + mainObj.Validation.Must_include);
                    report.Add("  Параметр Must_Include в соединяемом item: " + splittingObj.Validation.Must_include);
                }
                else
                    report.Add("Параметр Must_Include идентичны: ");

                report.Add("");

                if (!mainObj.Validation.Must_include.Equals(splittingObj.Validation.Must_include))
                {
                    report.Add("  Параметр Validation_Must_Include_One_Of в основном item: " + mainObj.Validation.Must_include_one_of);
                    report.Add("  Параметр Validation_Must_Include_One_Of в соединяемом item: " + splittingObj.Validation.Must_include_one_of);
                }
                else
                    report.Add("Параметр Must_Include_One_Of идентичны: ");

                report.Add("");

                if (!mainObj.Validation.Must_include.Equals(splittingObj.Validation.Must_include))
                {
                    report.Add("  Параметр Must_Not_Include в основном item: " + mainObj.Validation.Must_not_include);
                    report.Add("  Параметр Must_Not_Include в соединяемом item: " + splittingObj.Validation.Must_not_include);
                }
                else
                    report.Add("Параметр Must_Not_Include идентичны: ");
            }
            else
            {
                report.Add("Параметры Validation не были изменены, иные параметры были изменены");
            }

            return report;
        }

        private List<string> AddNewElement(List<JSON_FSSs> list)
        {
            string separator = new string('*', 20);

            List<string> report = new List<string>
            {
                "Были добавлены уникальные item: ",
                    separator
            };

            JSON_FSSs item;
            for (int i = 0; i < list.Count; i++)
            {
                item = list[i];
                report.Add((i + 1) + " элемент");
                report.Add("Домены: " + string.Join(", ", item.Domains));

                report.Add(item.Validation.Must_include.Count() > 0 ?
                        "Must_Include: " + string.Join("", item.Validation.Must_include) : "");

                report.Add(item.Validation.Must_include_one_of.Count() > 0 ?
                    "Must_Include_one_of: " + string.Join("", item.Validation.Must_include_one_of) : "");

                report.Add(item.Validation.Must_not_include.Count() > 0 ?
                    "Must__Not_Include: " + string.Join("", item.Validation.Must_not_include) : "");
            }

            return report;
        }

        private List<string> AddNewElement(List<JSON_site> list)
        {
            string separator = new string('*', 20);

            List<string> report = new List<string>
            {
                "Были добавлены уникальные item: ",
                    separator
            };

            JSON_site item;
            for (int i = 0; i < list.Count; i++)
            {
                item = list[i];
                report.Add((i + 1) + " элемент");
                report.Add("Домены: " + string.Join(", ", item.Domains));

                if (item.FSS_search_enabled)
                {
                    report.Add("FSS_search_interval_from: " + item.FSS_search_interval.From);
                    report.Add("FSS_search_interval_to: " + item.FSS_search_interval.To);
                }
                else
                {
                    report.Add("FSS_search_enabled - false");
                }

                report.Add("Validation_interval_from: " + item.Validation_interval.From);
                report.Add("Validation_interval_to: " + item.Validation_interval.To);

                if (item.Phrase_search != null)
                {
                    report.Add("Phrase_search_from: " + item.Phrase_search.Interval.From);
                    report.Add("Phrase_search_to: " + item.Phrase_search.Interval.To);

                    report.Add(item.Phrase_search.Must_include.Count() > 0 ? 
                        "Must_Include: " + string.Join("", item.Phrase_search.Must_include) : "");

                    report.Add(item.Phrase_search.Must_include_one_of.Count() > 0 ?
                        "Must_Include_one_of: " + string.Join("", item.Phrase_search.Must_include_one_of) : "");

                    report.Add(item.Phrase_search.Must_not_include.Count() > 0 ?
                        "Must__Not_Include: " + string.Join("", item.Phrase_search.Must_not_include) : "");
                }
            }

            return report;
        }

        private List<string> DifferenceBetweenObjects(JSON_site mainObj, JSON_site splittingObj)
        {
            string separator = new string('*', 20);

            List<string> report = new List<string>
                {
                    "Рассматриваются item с доменами: ",
                    "В главном файле:  " + string.Join(", ", mainObj.Domains),
                    "В соединяемом файле:  " + string.Join(", ", splittingObj.Domains),
                    separator
                };

            if (!mainObj.Validation_interval.Equals(splittingObj.Validation_interval))
            {
                report.Add("Параметры Validation_interval различаются: ");
                report.Add("  Параметр Validation_Interval_From в основном item: " + mainObj.Validation_interval.From);
                report.Add("  Параметр Validation_Interval_From в соединяемом item: " + splittingObj.Validation_interval.From);
                report.Add("");
                report.Add("  Параметр Validation_Interval_To в основном item: " + mainObj.Validation_interval.To);
                report.Add("  Параметр Validation_Interval_To в соединяемом item: " + splittingObj.Validation_interval.To);
            }
            else
            {
                report.Add("Параметры validation interval не были изменены, иные параметры были изменены");
            }

            return report;
        }

        //if (!Enumerable.SequenceEqual(mainObj.Domains, splittingObj.Domains))
        //{
        //    res.Add("Cписки доменов различаются: ");
        //    res.Add("Кол-во доменов в основном item: " + mainObj.Domains.Length);
        //    res.Add("Кол-во доменов в соединяемом item: " + splittingObj.Domains.Length);
        //    res.Add(separator);
        //else
        //{
        //    res.Add("Параметры Domains идентичны");
        //    res.Add(separator);
        //}

        public static void ShowException(Exception ex, string mes = "")
        {
            MessageBox.Show("Сообщение об ошибке: " + ex.Message +
                    "\n\n" +
                    "Source: " + ex.Source +
                    "\n\n" +
                    "Data: " + ex.Data +
                    "\n\n" +
                    "Метод: " + ex.TargetSite +
                    "\n\n" +
                    "StackTrace: " + ex.StackTrace +
                    "\n\n" +
                    "Операция не будет закончена" +
                    "\n\n" + 
                    mes, "Ошибка");
        }

        private void SplitFiles_Click(object sender, RoutedEventArgs e)
        {
            string Path;
            List<string> LogList;

            FileStream fileStream = null;
            StreamWriter strwrite = null;

            try
            {
                WriteOnFile(z =>
                {
                    // убираем полностью совпадающие элементы в двух последовательностях
                    List<JSON_site> RootSitesSplittingWithoutDublicate = RootSplitting.Sites.Except(Root.Sites, new JSON_site()).ToList();
                    List<JSON_FSSs> RootFSSSplittingWithoutDublicate = RootSplitting.FSSs.Except(Root.FSSs, new JSON_FSSs()).ToList();

                    if ((bool)Report.IsChecked)
                    {
                        Path = Directory.GetCurrentDirectory() + "/Отчет о соединении файлов " +
                                            System.IO.Path.GetFileName(OpenFileDlg.FileName).Replace(".txt", "") +
                                            " и " +
                                            System.IO.Path.GetFileName(OpenSplittingFileDlg.FileName);
                        fileStream = new FileStream(Path, FileMode.Create);
                        strwrite = new StreamWriter(fileStream);

                        if (RootFSSSplittingWithoutDublicate.Count > 0)
                            strwrite.WriteLine("##########Измененные FSS##########");
                    }

                    for (int j = 0; j < RootFSSSplittingWithoutDublicate.Count; j++)
                    {
                        foreach (var domainSplittingItem in RootFSSSplittingWithoutDublicate[j].Domains)
                        {
                            /*
                                Находим элемент в доменах которого находится один из доменов RootSplittingWithoutDublicate[j];
                                после замены, из второго списка удаляем элемент, на который заменили в пером списке и 
                                начинаем перечислять домены в RootSplittingWithoutDublicate[j] заново.
                            */

                            try
                            {
                                JSON_FSSs qwe = Root.FSSs.FirstOrDefault(x => x.Domains.Contains(domainSplittingItem));
                                if (qwe == null)
                                {
                                    continue;
                                }
                                else
                                {
                                    if ((bool)Report.IsChecked)
                                    {
                                        LogList = DifferenceBetweenObjects(Root.FSSs[Root.FSSs.IndexOf(qwe)], RootFSSSplittingWithoutDublicate[j]);

                                        foreach (var item in LogList)
                                        {
                                            strwrite.WriteLine(item);
                                        }
                                        strwrite.WriteLine("\r\n\r\n");
                                    }

                                    Root.FSSs[Root.FSSs.IndexOf(qwe)] = RootFSSSplittingWithoutDublicate[j];
                                    RootFSSSplittingWithoutDublicate.RemoveAt(j--);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                ShowException(ex);
                                return;
                            }
                        }
                    }

                    if (RootFSSSplittingWithoutDublicate.Count > 0)
                    {
                        // по логике остались только уникальные элементы, добавляем их в конец списка.
                        Root.FSSs.AddRange(RootFSSSplittingWithoutDublicate);

                        if ((bool)Report.IsChecked)
                        {
                            strwrite.WriteLine("##########Добавленные FSS##########");

                            LogList = AddNewElement(RootFSSSplittingWithoutDublicate);

                            foreach (var item in LogList)
                            {
                                strwrite.WriteLine(item);
                            }
                            strwrite.WriteLine("\r\n\r\n");
                        }

                        if (RootSitesSplittingWithoutDublicate.Count > 0)
                            strwrite.WriteLine("##########Измененные Sites##########");
                    }

                    // ищем item, из первого файла, в доменах которых находится хотябы один из доменов item второго файла.
                    for (int j = 0; j < RootSitesSplittingWithoutDublicate.Count; j++)
                    {
                        foreach (var domainSplittingItem in RootSitesSplittingWithoutDublicate[j].Domains)
                        {
                            /*
                                Находим элемент в доменах которого находится один из доменов RootSplittingWithoutDublicate[j];
                                после замены, из второго списка удаляем элемент, на который заменили в пером списке и 
                                начинаем перечислять домены в RootSplittingWithoutDublicate[j] заново.
                            */

                            try
                            {
                                JSON_site qwe = Root.Sites.FirstOrDefault(x => x.Domains.Contains(domainSplittingItem));
                                if (qwe == null)
                                {
                                    continue;
                                }
                                else
                                {
                                    if ((bool)Report.IsChecked)
                                    {
                                        LogList = DifferenceBetweenObjects(Root.Sites[Root.Sites.IndexOf(qwe)], RootSitesSplittingWithoutDublicate[j]);

                                        foreach (var item in LogList)
                                        {
                                            strwrite.WriteLine(item);
                                        }
                                        strwrite.WriteLine("\r\n\r\n");
                                    }

                                    Root.Sites[Root.Sites.IndexOf(qwe)] = RootSitesSplittingWithoutDublicate[j];
                                    RootSitesSplittingWithoutDublicate.RemoveAt(j--);
                                    break;
                                }
                            }
                            catch(Exception ex)
                            {
                                ShowException(ex);
                                return;
                            }
                        }
                    }

                    if (RootSitesSplittingWithoutDublicate.Count > 0)
                    {
                        // по логике остались только уникальные элементы, добавляем их в конец списка.
                        Root.Sites.AddRange(RootSitesSplittingWithoutDublicate);

                        if ((bool)Report.IsChecked)
                        {
                            strwrite.WriteLine("##########Добавленные Sites##########");

                            LogList = AddNewElement(RootSitesSplittingWithoutDublicate);

                            foreach (var item in LogList)
                            {
                                strwrite.WriteLine(item);
                            }
                            strwrite.WriteLine("\r\n\r\n");

                            strwrite.Dispose();
                            fileStream.Dispose();
                        }
                    }
                    
                }, OpenFileDlg, Root);
                ReloadData();
            }
            catch (Exception ex)
            {
                ShowException(ex);
                return;
            }
        }

        private void Switch_FSS_Sites(string cheker = "Sites")
        {
            ClearAll();
            FoundedItemsList.SelectedIndex = -1;
            Visibility visible = Visibility.Visible;
            if (cheker == "Sites")
            {
                Phrase_Search.Header = "Phrase__search";
                ShowALLElements(Root.Sites);
                CountOfAllElements.Content = "Всего элементов найдено в файле: " + Root.Sites.Count;
            }
            
            if (cheker == "FSS")
            {
                visible = Visibility.Hidden;
                Phrase_Search.Header = "Validation";
                ShowALLElements(Root.FSSs);
                CountOfAllElements.Content = "Всего элементов найдено в файле: " + Root.FSSs.Count;
            }

            FSS_Search.Visibility = visible;
            Validation_Interval.Visibility = visible;
            Phrase_Search_Interval_From.Visibility = visible;
            Phrase_Search_Interval_To.Visibility = visible;
            Phrase_Search_To_Label.Visibility = visible;
            Phrase_Search_From_Label.Visibility = visible;
        }

        private void Sites_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Switch_FSS_Sites();
        }

        private void FSS_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            Switch_FSS_Sites("FSS");
        }

        private void DomainsList_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(DomainsList.Text) && flag)
            {
                List<string> list = DomainsList.Text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                for (int i = 0; i < list.Count; i++)
                {
                    list[i] = OnlyDomain(list[i]);
                }

                DomainsList.Text = string.Join("\n", list.ToArray());
                DomainsList.Select(DomainsList.Text.Length, 0);
                flag = false;
            }
            else
                flag = true;
        }

        private void TextBoxSearch_Domain_GotFocus(object sender, RoutedEventArgs e)
        {
            isFocused = true;
        }

        private void TextBoxSearch_Domain_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (isFocused)
            {
                isFocused = false;
                (sender as TextBox).SelectAll();
            }
        }
    }
}

//Sites[Sites.IndexOf(qwe)] = new JSON_site
//{
//    Domains = RootSplittingWithoutDublicate[j].Domains,
//    FSS_search_enabled = RootSplittingWithoutDublicate[j].FSS_search_enabled,
//    FSS_search_interval = new Interval
//    {
//        From = RootSplittingWithoutDublicate[j].FSS_search_interval.From,
//        To = RootSplittingWithoutDublicate[j].FSS_search_interval.To
//    },
//    Phrase_search = RootSplittingWithoutDublicate[j].Phrase_search == null ? null : new Phrase_search
//    {
//        Interval = new Interval
//        {
//            From = RootSplittingWithoutDublicate[j].Phrase_search.Interval.From,
//            To = RootSplittingWithoutDublicate[j].Phrase_search.Interval.To
//        },
//        Must_include = RootSplittingWithoutDublicate[j].Phrase_search.Must_include,
//        Must_include_one_of = RootSplittingWithoutDublicate[j].Phrase_search.Must_include_one_of,
//        Must_not_include = RootSplittingWithoutDublicate[j].Phrase_search.Must_not_include
//    },
//    Validation_interval = new Interval
//    {
//        From = RootSplittingWithoutDublicate[j].Validation_interval.From,
//        To = RootSplittingWithoutDublicate[j].Validation_interval.To
//    }
//};