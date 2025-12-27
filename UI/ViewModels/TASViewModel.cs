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

namespace Mesen.ViewModels
{
	public class TASViewModel : DisposableViewModel
	{
		public ICommand ForwardCommand { get; }
		public ICommand RewindCommand { get; }

		public TASViewModel()
		{
			ForwardCommand = new RelayCommand(Forward);
			RewindCommand = new RelayCommand(Rewind);
		}

		private void Forward()
		{
			RecordApi.MovieAdvanceFrame();
		}

		private void Rewind()
		{
			RecordApi.MovieRewindFrame();

			RecordApi.MovieResume();
			RecordApi.MoviePauseOnNextFrame();
		}

	}
}
