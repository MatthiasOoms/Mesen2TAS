using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Mesen.Config;
using Mesen.Interop;
using Mesen.Utilities;
using Mesen.ViewModels;
using System.IO;

namespace Mesen.Windows
{
	public class TASRecordWindow : MesenWindow
	{

		public TASRecordWindow()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private async void OnBrowseClick1(object sender, RoutedEventArgs e)
		{
			string? filename = await FileDialogHelper.OpenFile(ConfigManager.MovieFolder, VisualRoot, FileDialogHelper.MesenTASExt);
			if(filename != null)
			{
				TASViewModel.LoadPath = filename;
			}
		}

		private async void OnBrowseClick(object sender, RoutedEventArgs e)
		{
			string? filename = await FileDialogHelper.SaveFile(ConfigManager.MovieFolder, EmuApi.GetRomInfo().GetRomName() + "." + FileDialogHelper.MesenTASExt, VisualRoot, FileDialogHelper.MesenTASExt);
			if(filename != null)
			{
			TASViewModel.SavePath = filename;
			}
		}

		private void Ok_OnClick(object sender, RoutedEventArgs e)
		{
			TASViewModel model = (TASViewModel)DataContext!;
			model.SaveConfig();

			Close(true);
		}

		private void Cancel_OnClick(object sender, RoutedEventArgs e)
		{
			Close(false);
		}
	}
}