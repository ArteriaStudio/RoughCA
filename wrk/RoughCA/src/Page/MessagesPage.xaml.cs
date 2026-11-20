using Arteria_s.DB;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	/// <summary>
	/// 利用者向けメッセージ
	/// </summary>
	public class Message : Data
	{
		public AppFacility	Facility;	//　分類
		public DateTime		CreateAt;	//　日時
		public string		DateAt;		//　
		public string		TimeAt;		//　
		public string		Text;		//　メッセージ
		public string		Which;		//　対象の識別子
		public string		Where;      //　対象の場所

		public Message(AppFacility eFacility, string pText, string pWhich, string pWhere = null)
		{
			Facility  = eFacility;
			CreateAt  = DateTime.Now;
			Text      = pText;
			Which     = pWhich ?? "";
			Where     = pWhere ?? "";

			DateAt = CreateAt.ToString("yyyy/MM/dd");
			TimeAt = CreateAt.ToString("HH:mm:ss.fff");
		}
		

		public override bool Validate()
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MessagesPage : Page
	{
		public ObservableCollection<Message>	m_pMessages = new ObservableCollection<Message>();
		private bool m_bExpanded = true;

		public MessagesPage()
		{
			this.InitializeComponent();
			UpdateCount();

#if ENABLE_TRACE
			for (var i = 0; i < 12; i++)
			{
				var pMessage = new Message(AppFacility.Error, "エラーです。サンプル文字列", "");
				pMessage.Text += string.Format("{0}", i);
				m_pMessages.Insert(0, pMessage);
			}
			UpdateCount();
#endif
		}

		private void UpdateCount()
		{
			if (LogCountText != null)
			{
				LogCountText.Text = m_pMessages.Count.ToString();
			}
		}

		//　利用者向けメッセージを追加
		public void	AddMessage(AppFacility eFacility, string pText, string pWhich, string pWhere)
		{
			var pMessage = new Message(eFacility, pText, pWhich, pWhere);
			m_pMessages.Insert(0, pMessage);
			UpdateCount();

			return;
		}
		public void AddMessage(Message pMessage)
		{
			m_pMessages.Insert(0, pMessage);
			UpdateCount();

			return;
		}

		private void ClearLogButton_Click(object sender, RoutedEventArgs e)
		{
			m_pMessages.Clear();
			UpdateCount();
		}

		private void ToggleExpandButton_Click(object sender, RoutedEventArgs e)
		{
			m_bExpanded = !m_bExpanded;
			LogListView.Visibility = m_bExpanded ? Visibility.Visible : Visibility.Collapsed;
			ToggleExpandIcon.Glyph = m_bExpanded ? "\uE70D" : "\uE70E";

			var pApp = App.Current as RoughCA.App;
			if (pApp?.m_pWindow is MainWindow pWindow)
			{
				pWindow.SetMessageLogExpanded(m_bExpanded);
			}
		}

		public static string GetFacilityLabel(AppFacility facility)
		{
			return facility switch
			{
				AppFacility.Complete => "COMPLETE",
				AppFacility.Error    => "ERROR",
				AppFacility.Info     => "INFO",
				_                    => "LOG"
			};
		}

		public static Brush GetFacilityBackground(AppFacility facility)
		{
			var isDark = Application.Current?.RequestedTheme == ApplicationTheme.Dark;
			return facility switch
			{
				AppFacility.Complete => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x2B, 0x2E, 0xA0, 0x43)
					: Windows.UI.Color.FromArgb(0x1F, 0x10, 0x7C, 0x10)),
				AppFacility.Error => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x2B, 0xFF, 0x99, 0xA4)
					: Windows.UI.Color.FromArgb(0x1F, 0xC4, 0x2B, 0x1C)),
				AppFacility.Info => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x29, 0x60, 0xCD, 0xFF)
					: Windows.UI.Color.FromArgb(0x1A, 0x00, 0x67, 0xC0)),
				_ => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)
					: Windows.UI.Color.FromArgb(0x14, 0x00, 0x00, 0x00))
			};
		}

		public static Brush GetFacilityForeground(AppFacility facility)
		{
			var isDark = Application.Current?.RequestedTheme == ApplicationTheme.Dark;
			return facility switch
			{
				AppFacility.Complete => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0x6C, 0xCB, 0x5F)
					: Windows.UI.Color.FromArgb(0xFF, 0x0E, 0x6A, 0x0E)),
				AppFacility.Error => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4)
					: Windows.UI.Color.FromArgb(0xFF, 0xB4, 0x23, 0x18)),
				AppFacility.Info => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0x7D, 0xD3, 0xFC)
					: Windows.UI.Color.FromArgb(0xFF, 0x00, 0x5A, 0x9E)),
				_ => new SolidColorBrush(isDark
					? Windows.UI.Color.FromArgb(0xFF, 0xC8, 0xC8, 0xC8)
					: Windows.UI.Color.FromArgb(0xFF, 0x52, 0x52, 0x52))
			};
		}
	}
}
