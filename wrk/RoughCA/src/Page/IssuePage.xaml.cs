using Arteria_s.DB;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	public class CertInputForm : Data
	{
		public string m_pCommonName;
		public string m_pHostName;
		public string m_pMailAddress;

		public CertInputForm()
		{
			m_pCommonName  = "";
			m_pHostName    = "";
			m_pMailAddress = "";
		}

		public override bool Validate()
		{
			if (IsNotNull(m_pCommonName) == false)
			{
				return (false);
			}
			if (IsNotNull(m_pHostName) == false)
			{
				return (false);
			}
			if (IsNotNull(m_pMailAddress) == false)
			{
				return (false);
			}

			return (true);
		}
	}

	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class IssuePage : Page
	{
		CertInputForm	m_pForm = new CertInputForm();

		public IssuePage()
		{
			this.InitializeComponent();

			UpdateFormState();
		}

		//　フォームの入力値を検査
		private bool IsValidForm()
		{
			bool IsEnabled = true;

			if (CertificateType.SelectedIndex == -1)
			{
				IsEnabled = false;
			}
			else if (Data.IsValidCommonName(CommonName.Text) == false)
			{
				IsEnabled = false;
			}
			else
			{
				//　サーバ証明書
				if (CertificateType.SelectedIndex == 0)
				{
					if (Data.IsValidFQDN(HostName.Text) == false)
					{
						IsEnabled = false;
					}
				}
				//　メール証明書
				else if (CertificateType.SelectedIndex == 1)
				{
					if (Data.IsValidMail(MailAddress.Text) == false)
					{
						IsEnabled = false;
					}
				}
				//　署名要求
				else if (CertificateType.SelectedIndex == 2)
				{
					if (Data.IsValidFQDN(HostName.Text) == false)
					{
						IsEnabled = false;
					}
					else if (Data.IsValidMail(MailAddress.Text) == false)
					{
						IsEnabled = false;
					}
				}
				else
				{
					IsEnabled = false;
				}
			}

			return (IsEnabled);
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			IssueButton.IsEnabled = IsValidForm();
		}

		private void CertificateType_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			IssueButton.IsEnabled = IsValidForm();

			UpdateFormState();
		}

		private void UpdateFormState()
		{
			switch (CertificateType.SelectedIndex)
			{
			case 0:
				//　サーバー証明書にメールアドレスは不要
				CommonName.IsEnabled = true;
				HostName.IsEnabled = true;
				MailAddress.IsEnabled = false;
				break;
			case 1:
				//　クライアント証明書にFQDNは不要
				CommonName.IsEnabled = true;
				HostName.IsEnabled = false;
				MailAddress.IsEnabled = true;
				break;
			case -1:
				CommonName.IsEnabled = false;
				HostName.IsEnabled = false;
				MailAddress.IsEnabled = false;
				break;
			}
		}

		//　証明書発行ボタンをクリック
		private void IssueButton_Click(object sender, RoutedEventArgs e)
		{
			if (IsValidForm() == false)
			{
				return;
			}
			var pApp = App.Current as RoughCA.App;
			var pWindow = pApp.m_pWindow as MainWindow;
			var pProfile = pApp.m_pProfile;
			var pSQLContext = pApp.GetSQLContext();
			var pAuthority = Authority.Instance;

			try
			{
				switch (CertificateType.SelectedIndex)
				{
				case 0:
					//　サーバ証明書
					pAuthority.CreateForServer(pSQLContext, m_pForm.m_pCommonName, m_pForm.m_pHostName, false, true);
					break;
				case 1:
					//　メール証明書
					pAuthority.CreateForClient(pSQLContext, m_pForm.m_pCommonName, m_pForm.m_pMailAddress, false, true);
					break;
				}

				pWindow.AddMessage(new Message(AppFacility.Complete, "証明書を発行しました。", m_pForm.m_pCommonName));
			}
			catch (AppException pException)
			{
				pWindow.AddMessage(new Message(pException.m_eFacility, pException.GetText(), pException.GetParameter()));
			}
		}
	}
}
