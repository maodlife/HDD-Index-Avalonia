using System;
using Avalonia;
using Avalonia.Controls;

namespace HDD_Index.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        NativeMenu.SetMenu(Application.Current, NativeMenu.GetMenu(this));
    }
}