using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using Avalonia.Input;
using Mesen.ViewModels;
using Mesen.GUI.Utilities;
using Mesen.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mesen.Interop;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Controls.Primitives;

namespace Mesen.Windows
{
	public class TASWindow : MesenWindow
	{
		private int rows = 1;
		private int columns = 10;
		private int size = 20;

		public TASWindow()
		{
			InitializeComponent();
			InitializeGrid();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void InitializeGrid()
		{
			var grid = this.FindControl<Grid>("GameGrid");

			if(grid == null)
			{
				return;
			}

			grid.RowDefinitions.Clear();
			grid.ColumnDefinitions.Clear();
			grid.Children.Clear();

			// Define rows and columns
			for(int i = 0; i < rows; i++)
			{
				grid.RowDefinitions.Add(new RowDefinition(new GridLength(size)));
			}
			for(int j = 0; j < columns; j++)
			{
				grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(size)));
			}

			// Add TextBlocks
			for(int i = 0; i < rows; i++) 
			{
				for(int j = 0; j < columns; j++) 
				{
					var cell = new ToggleButton {
						Classes = { "tas-cell" },
						Width = size,
						Height = size,
					};

					Grid.SetRow(cell, i);
					Grid.SetColumn(cell, j);

					// Handle click
					cell.Click += (s, e) =>
					{
						if(cell.IsChecked == true)
							cell.Content = "X";
						else
							cell.Content = ""; // optional, to toggle off
					};

					grid.Children.Add(cell);
				}
			}
		}
	}
}