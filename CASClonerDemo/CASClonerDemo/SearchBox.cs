using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace CASClonerDemo
{
    public class SearchDelayedTextBox : TextBox
    {
        public DispatcherTimer DelayTimer { get; set; }
        public delegate void OnInputFinished(object sender, EventArgs e);
        public event OnInputFinished InputFinished;

        public SearchDelayedTextBox()
            : base()
        {
            this.DelayTimer = null; // user is not typing
            this.KeyUp += Text_Changed;
            this.GotFocus += SearchDelayedTextBox_GotFocus;
            this.LostFocus += SearchDelayedTextBox_LostFocus;
            
        }

        void SearchDelayedTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(this.Text))
            {
                this.Dispatcher.Invoke((Action)(() =>
                {
                    this.Text = this.PlaceHolder;
                }));
            }
        }

        void SearchDelayedTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if(this.Text.Equals(this.PlaceHolder))
            {
                this.Dispatcher.Invoke((Action)(() => {
                    this.Text = string.Empty;
                }));
            }
        }



        private void DelayedTextBoxTimer_Tick(object sender, EventArgs e)
        {
            InputFinished(this, new SearchDelayedTextEventArgs(this.Text));
            this.DelayTimer.Stop();
            this.DelayTimer = null;
        }

        public class SearchDelayedTextEventArgs : EventArgs
        {
            public string Text { get; set; }

            public SearchDelayedTextEventArgs(string text)
            {
                this.Text = text;
            }
        }

        private void Text_Changed(object sender, EventArgs e)
        {
            if(this.DelayTimer != null) // user is still typing
            {
                if(this.DelayTimer.Interval.Milliseconds < 900)
                {
                    this.DelayTimer.Interval.Add(new TimeSpan(900));
                }
            }
            else
            {
                this.DelayTimer = new DispatcherTimer();
                this.DelayTimer.Interval = new TimeSpan(900);
                this.DelayTimer.Tick += DelayedTextBoxTimer_Tick;
                this.DelayTimer.Start();

            }
        }

        [Description("The default text to show when the textbox is not focused and empty")]
        public string PlaceHolder { get; set; }

        
        
        
    }
}
