using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using InfiniteSpace.Native;

namespace InfiniteSpace.Shell;

internal sealed class TrayIcon : IDisposable
{
	private readonly NotifyIcon _icon;

	private readonly Icon _image;

	private readonly nint _imageHandle;

	public TrayIcon(TrayActions actions)
	{
		ToolStripMenuItem autostart = new ToolStripMenuItem("Start with Windows");
		autostart.Click += (object? _, EventArgs _) =>
		{
			actions.SetAutoStart(!actions.IsAutoStartEnabled());
		};
		ToolStripMenuItem theme = new ToolStripMenuItem("Theme");
		foreach (string name in actions.Themes)
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(name);
			toolStripMenuItem.Click += (object? _, EventArgs _) =>
			{
				actions.SetTheme(name);
			};
			theme.DropDownItems.Add(toolStripMenuItem);
		}
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add(Item("Draw a space", "Ctrl+Alt+S", actions.Draw));
		contextMenuStrip.Items.Add(Item("Spawn a block", "Ctrl+Alt+B", actions.Block));
		contextMenuStrip.Items.Add(Item("Move a space", "Ctrl+Alt+M", actions.Move));
		contextMenuStrip.Items.Add(Item("Remove newest space", "Ctrl+Alt+E", actions.Remove));
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Item("Ink colour and size…", "Right-click a space", actions.Ink));
		contextMenuStrip.Items.Add(Item("Erase with brush…", null, actions.EraseWithBrush));
		contextMenuStrip.Items.Add(Item("Erase all spaces…", null, actions.EraseAll));
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(theme);
		contextMenuStrip.Items.Add(autostart);
		contextMenuStrip.Items.Add(Item("Open data folder", null, actions.OpenDataFolder));
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Item("Exit", null, actions.Exit));
		contextMenuStrip.Opening += (object? _, CancelEventArgs _) =>
		{
			autostart.Checked = actions.IsAutoStartEnabled();
			string text = actions.CurrentTheme();
			foreach (ToolStripMenuItem dropDownItem in theme.DropDownItems)
			{
				dropDownItem.Checked = dropDownItem.Text == text;
			}
		};
		_imageHandle = CreateIconHandle();
		_image = Icon.FromHandle(_imageHandle);
		_icon = new NotifyIcon
		{
			Icon = _image,
			Text = "Infinite Space",
			ContextMenuStrip = contextMenuStrip,
			Visible = true
		};
		_icon.MouseDoubleClick += (object? _, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				actions.Draw();
			}
		};
	}

	public void Notify(string title, string text)
	{
		_icon.ShowBalloonTip(5000, title, text, ToolTipIcon.None);
	}

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.ContextMenuStrip?.Dispose();
		_icon.Dispose();
		_image.Dispose();
		Win32.DestroyIcon(_imageHandle);
	}

	private static ToolStripMenuItem Item(string text, string? shortcut, Action action)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text);
		toolStripMenuItem.ShortcutKeyDisplayString = shortcut;
		toolStripMenuItem.Click += (object? _, EventArgs _) =>
		{
			action();
		};
		return toolStripMenuItem;
	}

	private static nint CreateIconHandle()
	{
		using (Bitmap bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
		{
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.Transparent);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					_003C_003Ey__InlineArray7<Point> buffer = default;
					buffer[0] = new Point(3, 15);
					buffer[1] = new Point(8, 4);
					buffer[2] = new Point(20, 3);
					buffer[3] = new Point(29, 9);
					buffer[4] = new Point(28, 22);
					buffer[5] = new Point(18, 29);
					buffer[6] = new Point(6, 26);
					graphicsPath.AddClosedCurve(buffer, 0.5f);
					graphics.FillPath(Brushes.Black, graphicsPath);
					using Pen pen = new Pen(Color.White, 2.5f);
					graphics.DrawPath(pen, graphicsPath);
				}
				graphics.SmoothingMode = SmoothingMode.None;
				Plus(graphics, Color.FromArgb(255, 239, 17), 11, 12);
				Plus(graphics, Color.FromArgb(95, 205, 228), 20, 19);
				Dot(graphics, Color.FromArgb(255, 0, 208), 20, 9);
				Dot(graphics, Color.FromArgb(8, 163, 16), 11, 21);
			}
			return bitmap.GetHicon();
		}
		static void Dot(Graphics g, Color colour, int x, int y)
		{
			using SolidBrush brush = new SolidBrush(colour);
			g.FillRectangle(brush, x - 1, y - 1, 2, 2);
		}
		static void Plus(Graphics g, Color colour, int x, int y)
		{
			using SolidBrush brush = new SolidBrush(colour);
			g.FillRectangle(brush, x - 1, y - 3, 2, 6);
			g.FillRectangle(brush, x - 3, y - 1, 6, 2);
		}
	}
}
