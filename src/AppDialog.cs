using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Callrift {
    public static class AppDialog {
        public static bool Confirm(Window owner,string title,string message,string confirm) { return Show(owner,title,message,confirm,true); }
        public static void Alert(Window owner,string title,string message) { Show(owner,title,message,"OK",false); }
        static bool Show(Window owner,string title,string message,string confirm,bool question) {
            var dialog=new Window { Title=title,Width=480,MaxHeight=SystemParameters.WorkArea.Height-40,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=owner,FontFamily=owner.FontFamily,FontSize=13,Resources=owner.Resources };
            dialog.SetResourceReference(Window.BackgroundProperty,"Card"); WindowFrame.Attach(dialog);
            var border=new Border { Padding=new Thickness(24),BorderThickness=new Thickness(1) }; border.SetResourceReference(Border.BackgroundProperty,"Card"); border.SetResourceReference(Border.BorderBrushProperty,"Line");
            var stack=new StackPanel(); border.Child=stack;
            var heading=new TextBlock { Text=title,FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap }; heading.SetResourceReference(TextBlock.ForegroundProperty,"Fg"); stack.Children.Add(heading);
            var text=new TextBlock { Text=message,TextWrapping=TextWrapping.Wrap,LineHeight=21 }; text.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
            stack.Children.Add(new ScrollViewer { Content=text,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(120,Math.Min(380,SystemParameters.WorkArea.Height-240)),Margin=new Thickness(0,12,0,24) });
            var actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right }; stack.Children.Add(actions);
            if(question) { var cancel=new Button { Content=I18n.T("NotNow"),IsCancel=true,IsDefault=true,Margin=new Thickness(0,0,10,0) }; cancel.Click+=(s,e)=>dialog.DialogResult=false; actions.Children.Add(cancel); }
            var ok=new Button { Content=confirm,IsDefault=!question }; ok.SetResourceReference(FrameworkElement.StyleProperty,"Primary"); ok.Click+=(s,e)=>dialog.DialogResult=true; actions.Children.Add(ok);
            dialog.Content=border;
            dialog.PreviewKeyDown+=(s,e)=>{ if(e.Key==Key.Escape) { dialog.DialogResult=false; e.Handled=true; } };
            return dialog.ShowDialog()==true;
        }
    }
}
