using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mesen.Interop;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using System.Threading;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using Mesen.ViewModels;
using Avalonia.Rendering;

namespace Mesen.Windows
{
	public class TASWindow : MesenWindow
	{
		private readonly CancellationTokenSource _cts = new();

		private Grid m_Grid;
		private NativeRenderer _renderer;

		private int m_Rows = 0;
		private int m_Columns = 0;
		private int m_GridRows = 0;
		private const int m_GridCols = 12;

		private DispatcherTimer _frameTimer;
		private int m_PreviousFrame = -1;
		private int m_CurrentFrame = 0;

		private const int m_MinRows = 20;

		private List<Image> m_RowMarkerImages = new();

		//(|,Reset,Power,|,U,D,L,R,Select,Start,B,A)
		string[] m_Tags =
					{
						"",		// 0 (spacer)
						"Frame",	// 1
						"reset",	// 2
						"Power",	// 3
						"U",		// 4
						"D",		// 5
						"L",		// 6
						"R",		// 7
						"Start",	// 8
						"select",// 9
						"B",		// 10
						"A",		// 11
					};

		double m_Height = 20;
		double[] m_Widths =
		{
				20,			// 0
				20 * 2.5,	// 1
				20 * 2.5,	// 2
				20 * 2.5,	// 3
				20,			// 4
				20,			// 5
				20,			// 6
				20,			// 7
				20 * 2.5,	// 8
				20 * 2.5,	// 9
				20,			// 10
				20,			// 11
			};

		public TASWindow()
		{
			InitializeComponent();

			if(Design.IsDesignMode)
				return;

			InitializeGrid();
			StartFrameLoop();

			_renderer = this.GetControl<NativeRenderer>("Renderer");
		}

		private void StartFrameLoop()
		{
			_frameTimer = new DispatcherTimer {
				Interval = TimeSpan.FromMilliseconds(16) // ~60 Hz
			};

			_frameTimer.Tick += (_, _) => UpdateCurrentFrame();
			_frameTimer.Start();
		}

		protected override void OnClosed(EventArgs e)
		{
			HistoryApi.HistoryViewerInitialize(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, _renderer.Handle);
			if (HistoryApi.HistoryViewerEnabled())
			{
				HistoryApi.HistoryViewerSaveMovie(TASViewModel.SavePath, 0, RecordApi.MovieGetFrameCount());
			}
			HistoryApi.HistoryViewerRelease();

			_frameTimer?.Stop();

			_cts.Cancel();
			_cts.Dispose();
			RecordApi.MovieStop();
			base.OnClosed(e);
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void InitializeGrid()
		{
			m_Grid = this.FindControl<Grid>("GameGrid");
			if(m_Grid == null) {
				return;
			}

			m_Grid.RowDefinitions.Clear();
			m_Grid.ColumnDefinitions.Clear();
			m_Grid.Children.Clear();

			// Define header row
			m_Grid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));

			// Add header TextBlocks
			for(int j = 0; j < m_GridCols; j++) {
				var headerText = new TextBlock {
					Classes = { "tas-header" },
					Text = m_Tags[j],
					TextAlignment = Avalonia.Media.TextAlignment.Center
				};

				var header = new Border {
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(1),
					Padding = new Thickness(0),
					Child = headerText,
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
				};

				m_Grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(m_Widths[j])));

				Grid.SetRow(header, 0);
				Grid.SetColumn(header, j);
				m_Grid.Children.Add(header);
			}

			if(EmuApi.IsPaused()) {
				return;
			}

			while(!RecordApi.MoviePlaying()) {
				// Wait
				Task.Delay(50).Wait();
			}

			m_Rows = RecordApi.MovieGetInputRowCount();
			m_Columns = RecordApi.MovieGetInputColCount();

			string[][] inputs = new string[m_MinRows][];
			for(int r = 0; r < m_MinRows; r++) {
				inputs[r] = new string[m_Columns];
				for(int c = 0; c < m_Columns; c++) {
					inputs[r][c] = RecordApi.MovieGetInputCell(r, c);
				}
			}

			// Add existing rows from movie
			// A full empty row looks like this: |..|........
			// Where . is an empty cell, and each character represents a button
			// This is the order: (|,Reset,Power,|,U,D,L,R,Select,Start,B,A)
			for(int r = 0; r < m_MinRows; r++) {
				bool[] values = new bool[m_GridCols - 2];
				string rowValue = "";
				for(int c = 0; c < m_Columns; c++) {
					// Go over each char in input[r][c]
					rowValue += inputs[r][c];
				}

				for(int charIdx = 0; charIdx < rowValue.Length; charIdx++) {
					char ch = rowValue[charIdx];
					values[charIdx] = ch != '.' && ch != '|';
				}
				AddRow(values);
			}
		}

		private void AddRow(bool[] values)
		{
			if(values.Length != m_GridCols - 2) {
				throw new ArgumentException("Values array length must be equal to number of columns minus 2 (for frame and spacer)");
			}

			m_GridRows++;
			m_Grid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));

			// Add new row of cells
			for(int j = 0; j < m_GridCols; j++) {
				string tag = "";

				if(j == 0) {
					tag = "";
				} else if(j == 1) {
					tag = (m_GridRows).ToString().PadLeft(6, '0');
				} else {
					tag = m_Tags[j];
				}

				var header = new Border {
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(1),
					Padding = new Thickness(0),
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
				};

				if(j < 1) {
					// Create an image display for the spacer column
					var cell = new Image {
						Tag = m_GridRows,
						Width = m_Widths[j],
						Height = m_Height,
						Stretch = Stretch.Uniform,
						HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
						VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
						Source = LoadBitmap("avares://Mesen/Assets/MediaPlay.png")
					};

					m_RowMarkerImages.Add(cell);
					cell.IsVisible = false;
					header.Child = cell;

					Grid.SetRow(header, m_GridRows);
					Grid.SetColumn(header, j);
					m_Grid.Children.Add(header);
				} else if(j == 1) {
					// Create a ToggleButton for other columns
					var cell = new Button {
						Classes = { "tas-cell" },
						Width = m_Widths[j],
						Height = m_Height,
						Tag = tag,
						Content = "",
						HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
						VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
					};

					cell.Content = cell.Tag;

					cell.Click += (s, e) => {
						// String to int
						int goalFrame = int.Parse((string)cell.Tag);
						RecordApi.MovieJumpToFrame(goalFrame);
						RecordApi.MovieResume();

						m_PreviousFrame = m_CurrentFrame;
						m_CurrentFrame = goalFrame;

						UpdateFrameMarker();
					};

					header.Child = cell;

					Grid.SetRow(header, m_GridRows);
					Grid.SetColumn(header, j);
					m_Grid.Children.Add(header);
				} else {
					// Create a ToggleButton for other columns
					var cell = new ToggleButton {
						Classes = { "tas-cell" },
						Width = m_Widths[j],
						Height = m_Height,
						Tag = tag,
						Content = "",
						HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
						VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
					};

					cell.Content = values[j - 2] ? cell.Tag : "";
					cell.IsChecked = values[j - 2];

					header.Child = cell;

					// Handle click
					int rowIndex = m_GridRows;   // capture row
					int colIndex = j;           // capture column

					cell.IsCheckedChanged += (s, e) => {
						int inputHalfIdx = 0;
						int fieldIdx = colIndex - 2;

						if(colIndex > 3) {
							inputHalfIdx = 1;
							fieldIdx = colIndex - 4;
						}

						if(cell.IsChecked == true) {
							cell.Content = cell.Tag;
							RecordApi.MovieSetInputCell(rowIndex, inputHalfIdx, fieldIdx, ((string)cell.Tag)[0]);
						} else {
							cell.Content = "";
							RecordApi.MovieSetInputCell(rowIndex, inputHalfIdx, fieldIdx, '.');
						}
					};

					Grid.SetRow(header, m_GridRows);
					Grid.SetColumn(header, j);
					m_Grid.Children.Add(header);
				}
			}
		}

		private void UpdateCurrentFrame()
		{
			if(EmuApi.IsPaused())
				return;

			if(m_CurrentFrame >= m_RowMarkerImages.Count)
				return;

			if(m_CurrentFrame == m_PreviousFrame)
				return;

			m_PreviousFrame = m_CurrentFrame;
			m_CurrentFrame = (int)RecordApi.MovieGetFrameCount();

			UpdateFrameMarker();
		}

		private void UpdateFrameMarker()
		{
			if(m_CurrentFrame >= m_RowMarkerImages.Count)
				return;

			if(m_CurrentFrame == m_PreviousFrame)
				return;

			// Disable previous markers
			int range = 40;
			int lowerBound = Math.Max(0, m_CurrentFrame - range);
			int upperBound = m_CurrentFrame - lowerBound - 1;
			if(upperBound > 0) {
				foreach(Image child in m_RowMarkerImages.GetRange(lowerBound, upperBound)) {
					child.IsVisible = false;
				}
			}

			var newChild = m_RowMarkerImages[m_CurrentFrame];
			if(newChild != null) {
				newChild.IsVisible = true;
			}

			if(m_PreviousFrame > m_RowMarkerImages.Count || m_PreviousFrame < 0)
				return;

			var oldChild = m_RowMarkerImages[m_PreviousFrame];
			if(oldChild != null) {
				oldChild.IsVisible = false;
			}
		}

		private static Bitmap LoadBitmap(string uri)
		{
			using var stream = AssetLoader.Open(new Uri(uri));
			return new Bitmap(stream);
		}

		private void AddBlankRow()
		{
			bool[] values = new bool[m_GridCols - 2];
			AddRow(values);
		}

		public async Task AddMissingRowsAsync()
		{
			RecordApi.MoviePause();

			var token = _cts.Token;

			List<bool[]> rows;

			try {
				rows = await Task.Run(() => {
					var result = new List<bool[]>(m_Rows - m_MinRows);

					for(int r = m_MinRows; r < m_Rows; r++) {
						if(token.IsCancellationRequested)
							return result;

						bool[] values = new bool[m_GridCols - 2];
						string rowValue = "";

						for(int c = 0; c < m_Columns; c++) {
							if(token.IsCancellationRequested)
								return result;

							rowValue += RecordApi.MovieGetInputCell(r, c);
						}

						for(int i = 0; i < rowValue.Length; i++) {
							values[i] = rowValue[i] != '.' && rowValue[i] != '|';
						}

						result.Add(values);
					}

					return result;
				}, token);
			} catch(OperationCanceledException) {
				return;
			}

			if(token.IsCancellationRequested) {
				return;
			}

			foreach(var values in rows) {
				if(token.IsCancellationRequested)
					break;

				await Dispatcher.UIThread.InvokeAsync(() => AddRow(values), DispatcherPriority.Background, token);

				// Yield to UI to keep it responsive
				await Task.Yield();
			}

			if(!token.IsCancellationRequested)
				RecordApi.MovieResume();
		}

		private void Forward_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			m_PreviousFrame = m_CurrentFrame;
			m_CurrentFrame = (int)RecordApi.MovieGetFrameCount();

			UpdateFrameMarker();
		}
	}
}