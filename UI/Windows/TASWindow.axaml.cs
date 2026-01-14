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
using System.Linq;

namespace Mesen.Windows
{
	public class TASWindow : MesenWindow
	{
		private readonly CancellationTokenSource _cts = new();

		private Grid m_GameGrid;
		private Grid m_MacroGrid;
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

		private int m_MacroRows = 5;
		private bool[][] m_MacroValues;

		private TASViewModel VM => DataContext as TASViewModel;

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
				// Get 60fps framerate
				Interval = TimeSpan.FromMilliseconds(1 / EmuApi.GetFps())
			};

			_frameTimer.Tick += (_, _) => UpdateCurrentFrame();
			_frameTimer.Start();
		}

		protected override void OnClosed(EventArgs e)
		{
			HistoryApi.HistoryViewerInitialize(TryGetPlatformHandle()?.Handle ?? IntPtr.Zero, _renderer.Handle);
			if(HistoryApi.HistoryViewerEnabled()) {
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
			m_MacroValues = new bool[m_MacroRows][];
			for(int r = 0; r < m_MacroRows; r++)
			{
				m_MacroValues[r] = new bool[m_GridCols - 2];
			}

			m_GameGrid = this.FindControl<Grid>("GameGrid");
			if(m_GameGrid == null) {
				return;
			}

			m_MacroGrid = this.FindControl<Grid>("MacroGrid");
			if(m_MacroGrid == null) {
				return;
			}

			m_GameGrid.RowDefinitions.Clear();
			m_GameGrid.ColumnDefinitions.Clear();
			m_GameGrid.Children.Clear();

			// Define header rows
			m_MacroGrid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));
			m_GameGrid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));

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

				Grid.SetRow(header, 0);
				Grid.SetColumn(header, j);

				m_GameGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(m_Widths[j])));

				m_GameGrid.Children.Add(header);

				if(j < 2) {
					continue;
				}

				string text = m_Tags[j][0].ToString();

				var macroHeaderText = new TextBlock {
					Classes = { "tas-header" },
					Text = text,
					TextAlignment = Avalonia.Media.TextAlignment.Center
				};

				var macroHeader = new Border {
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(1),
					Padding = new Thickness(0),
					Child = macroHeaderText,
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
				};

				Grid.SetRow(macroHeader, 0);
				Grid.SetColumn(macroHeader, j - 2);

				m_MacroGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(m_Widths[0])));

				m_MacroGrid.Children.Add(macroHeader);
			}

			// Add 5 rows to the Macro Grid
			for(int r = 0; r < m_MacroRows; r++)
			{
				m_MacroGrid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));

				for(int c = 0; c < m_GridCols - 2; c++)
				{
					var cell = new ToggleButton
					{
						Classes = { "tas-cell" },
						Width = m_Widths[0],
						Height = m_Height,
						Tag = m_Tags[c + 2],
						Content = "",
						HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
						VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
					};

					var header = new Border
					{
						BorderBrush = Brushes.Black,
						BorderThickness = new Thickness(1),
						Padding = new Thickness(0),
						Child = cell,
						HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
						VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
					};

					cell.Content = "";
					cell.IsChecked = false;

					int rowIndex = r;

					int fieldIdx = c;
					int colIdx = c;

					cell.IsCheckedChanged += (s, e) => 
					{
						if(c > 1)
						{
							fieldIdx = c - 2;
						}

						if(cell.IsChecked == true)
						{
							var content = ((string)cell.Tag)[0];

							cell.Content = content;
						}
						else
						{
							cell.Content = "";
						}

						m_MacroValues[rowIndex][colIdx] = cell.IsChecked == true;
					};

					Grid.SetRow(header, r + 1);
					Grid.SetColumn(header, c);

					m_MacroGrid.Children.Add(header);
				}
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
			m_GameGrid.RowDefinitions.Add(new RowDefinition(new GridLength(m_Height)));

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
					m_GameGrid.Children.Add(header);
				} else if(j == 1) {
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
					m_GameGrid.Children.Add(header);
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
					m_GameGrid.Children.Add(header);
				}
			}
		}

		private void UpdateCurrentFrame()
		{
			if(m_CurrentFrame >= m_RowMarkerImages.Count)
				return;

			m_PreviousFrame = m_CurrentFrame;
			m_CurrentFrame = (int)RecordApi.MovieGetFrameCount();

			if(m_CurrentFrame == m_PreviousFrame)
				return;

			UpdateFrameMarker();
		}

		private void UpdateFrameMarker()
		{
			if(m_CurrentFrame >= m_RowMarkerImages.Count) {
				return;
			}

			if(m_CurrentFrame == m_PreviousFrame) {
				return;
			}

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

			// Get Row that corresponds to the current frame
			Control? rowControl = null;
			int targetFrame = m_CurrentFrame;
			int padding = 2;

			if(m_CurrentFrame + padding < m_RowMarkerImages.Count) {
				targetFrame += padding;
			}

			foreach(var child in m_GameGrid.Children) {
				if(Grid.GetRow(child) == targetFrame) {
					rowControl = child;
					break;
				}
			}

			// Scroll to make sure the row is visible
			if(rowControl != null && VM.FollowCursor == true) {
				rowControl.BringIntoView();
			}


			if(m_PreviousFrame > m_RowMarkerImages.Count || m_PreviousFrame < 0) {
				return;
			}

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

			while(m_Rows <= m_MinRows) {
				await Task.Delay(50);
			}

			try {
				rows = await Task.Run(() => {
					var result = new List<bool[]>(m_Rows - m_MinRows);

					for(int r = m_MinRows; r < m_Rows; r++) {
						if(token.IsCancellationRequested) {
							return result;
						}

						bool[] values = new bool[m_GridCols - 2];
						string rowValue = "";

						for(int c = 0; c < m_Columns; c++) {
							if(token.IsCancellationRequested) {
								return result;
							}

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
				if(token.IsCancellationRequested) {
					break;
				}

				await Dispatcher.UIThread.InvokeAsync(() => AddRow(values), DispatcherPriority.Background, token);

				// Yield to UI to keep it responsive
				await Task.Yield();
			}

			if(!token.IsCancellationRequested) {
				RecordApi.MovieResume();
			}
		}

		private void Forward_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			m_PreviousFrame = m_CurrentFrame;
			m_CurrentFrame = (int)RecordApi.MovieGetFrameCount();

			UpdateFrameMarker();
		}

		private void Paste_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// Get Frame MesenNumericTextbox
			var frameTextBox = this.FindControl<Controls.MesenNumericTextBox>("Frame");

			if(frameTextBox == null)
			{
				return;
			}

			if (frameTextBox.Text == null)
			{
				return;
			}

			if(frameTextBox.Text.Length < 1)
			{
				return;
			}

			// Read numeric value safely
			if(!int.TryParse(frameTextBox.Text, out int targetFrame))
			{
				return;
			}

			// Clamp to valid grid range
			int maxFrame = m_GridRows - m_MacroRows;
			targetFrame = Math.Clamp(targetFrame, 0, maxFrame);

			InsertInputs(targetFrame, m_MacroValues);
		}

		private void InsertInputs(int index, bool[][] values)
		{
			if(values.Length != m_MacroRows)
			{
				throw new ArgumentException("Values array length must be equal to number of macro rows");
			}

			for(int idx = 0; idx < values.Length; idx++)
			{
				if(values[idx].Length != m_GridCols - 2)
				{
					throw new ArgumentException("Values array length must be equal to number of columns minus 2 (for frame and spacer)");
				}

				int targetRow = index + idx;
				int fieldIdx = 0;

				for(int colIdx = 0; colIdx < m_GridCols - 2; colIdx++)
				{
					fieldIdx = colIdx;
					
					if(colIdx > 1)
					{
						fieldIdx -= 2;
					}
					
					var border = m_GameGrid.Children.FirstOrDefault(c => Grid.GetRow(c) == targetRow && Grid.GetColumn(c) == colIdx + 2) as Border;
					
					if(border == null)
					{
						continue;
					}

					var cell = border.Child as ToggleButton;

					if(cell == null)
					{
						continue;
					}

					if(values[idx][colIdx])
					{
						cell.IsChecked = true;
					}
					else
					{
						cell.IsChecked = false;
					}
				}
			}
		}
	}
}