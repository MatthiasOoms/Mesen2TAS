using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;
using Mesen.Config;
using System.IO;
using System.Xml.Serialization;
using Mesen.Utilities;
using System.Reflection;
using Avalonia.Controls.Selection;
using ReactiveUI;
using Mesen.Interop;
using Avalonia.Controls;
using System.IO.Compression;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Mesen.Controls;

namespace Mesen.ViewModels
{
	public class TASViewModel : ViewModelBase
	{
		public ICommand ForwardCommand { get; }
		public ICommand RewindCommand { get; }

		public static string LoadPath { get; set; } = Path.Join(ConfigManager.MovieFolder, EmuApi.GetRomInfo().GetRomName() + "." + FileDialogHelper.MesenTASExt);
		public static string SavePath { get; set; } = Path.Join(ConfigManager.MovieFolder, EmuApi.GetRomInfo().GetRomName() + "_out." + FileDialogHelper.MesenTASExt);
		[Reactive] public MovieRecordConfig Config { get; set; }

		public TASViewModel()
		{
			Config = ConfigManager.Config.TASRecord.Clone();

			ForwardCommand = new RelayCommand(Forward);
			RewindCommand = new RelayCommand(Rewind);
		}

		public void SaveConfig()
		{
			ConfigManager.Config.TASRecord = Config.Clone();
		}

		private void Forward()
		{
			RecordApi.MovieAdvanceFrame();
		}

		private void Rewind()
		{
			RecordApi.MovieRewindFrame();
			RecordApi.MovieAdvanceFrame();
		}
	}
}
