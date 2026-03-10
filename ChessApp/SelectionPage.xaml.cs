using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChessApp
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SelectionPage : ContentPage
    {
        public ObservableCollection<string> Items { get; set; }
        public string result { get; set; }

        public delegate void PopupClosedDelegate();
        public event PopupClosedDelegate PopupClosed;

        public SelectionPage(string title, string accept, string cancel, params string[] options)
        {
            InitializeComponent();

            result = null;

            label_title.Text = title;

            btn_accept.Text = accept;
            btn_accept.IsVisible = accept.Length > 0;

            btn_cancel.Text = cancel;
            btn_cancel.IsVisible = cancel.Length > 0;


            Items = new ObservableCollection<string>(options);

            listview.ItemsSource = Items;

            Application.Current.MainPage.Navigation.PushAsync(this);
        }

        private void Handle_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item == null)
                return;

            result = Items[e.ItemIndex];

            //Deselect Item
            ((ListView)sender).SelectedItem = null;

            if (PopupClosed != null)
            {
                PopupClosed();
            }
        }

        private void OnAccept(object sender, EventArgs args)
        {
            result = btn_accept.Text;

            if (PopupClosed != null)
            {
                PopupClosed();
            }
        }

        private void OnCancel(object sender, EventArgs args)
        {
            result = btn_cancel.Text;

            if (PopupClosed != null)
            {
                PopupClosed();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            result = null;

            if (PopupClosed != null)
            {
                PopupClosed();
            }

            return base.OnBackButtonPressed();
        }

        public Task<Page> Close()
        {
            // close
            return Application.Current.MainPage.Navigation.PopAsync();
        }

        public async Task<string> Select()
        {
            var waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset);

            this.PopupClosed += () =>
            {
                waitHandle.Set();
            };

            await Task.Run(() => waitHandle.WaitOne());

            return this.result;
        }

        public async static Task<string> Choose(string title, string accept, string cancel, params string[] options)
        {
            var waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset);

            SelectionPage sp = new SelectionPage(title, accept, cancel, options);

            sp.PopupClosed += () =>
            {
                waitHandle.Set();
            };

            await Task.Run(() => waitHandle.WaitOne());

            // close 
            if (sp.result != null) await Application.Current.MainPage.Navigation.PopAsync();

            return sp.result;
        }
    }
}
