using Arteria_s.DB.Base;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Arteria_s.App.RoughCA
{
	public class PrepareFlags
	{
		public bool bExistDbParams;		//　データベースとの接続情報が登録されている。
		public bool bExistOrgProfile;	//　
		public bool	bExistAuthority;	//　有効な認証局証明書が存在する。

		public PrepareFlags()
		{
			bExistDbParams   = false;
			bExistOrgProfile = false;
			bExistAuthority  = false;
		}

		//　環境の前提条件の状態を検査
		public void Check(VSQLContext pSQLContext, DbParams m_pDbParams, Authority pAuthority)
		{
			//　データベース接続情報が登録されているか？
			if (m_pDbParams == null)
			{
				bExistDbParams = false;
			}
			else
			{
				bExistDbParams = m_pDbParams.Validate();
			}
			if ((pAuthority == null) || (pAuthority.m_pOrgProfile == null))
			{
				bExistOrgProfile = false;
			}
			else
			{
				bExistOrgProfile = pAuthority.m_pOrgProfile.Validate();
			}

			//　有効な認証局証明書が存在するか？
			if ((pAuthority == null) || (pAuthority.Validate(pSQLContext) == false))
			{
				bExistAuthority = false;
			}
			else
			{
				bExistAuthority = true;
			}

			return;
		}
	}

	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// set to .csproj <WindowsAppSDKSelfContained>yes</WindowsAppSDKSelfContained>, if unable to run Unpackaged app.
		/// https://learn.microsoft.com/ja-jp/aspnet/core/host-and-deploy/visual-studio-publish-profiles?view=aspnetcore-8.0
		/// </summary>
		public App()
		{
			//　WinUI3かつUnpackagedのアプリケーションは、動的に表示言語を変更する機能はないと思われる。（2023/11/13：said stack overflow...）
			//Debug.WriteLine(Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride);
			//Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "ja-JP";
			//Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", "de-DE");
			//ApplicationLanguages.PrimaryLanguageOverride
			//Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "ja";

			this.InitializeComponent();
			//this.Exit += App_Exit;

			//　
			Debug.WriteLine(CultureInfo.CurrentUICulture.Name);


			//（これがマスター）
			//　https://learn.microsoft.com/ja-jp/windows/apps/windows-app-sdk/deploy-unpackaged-apps
			//　https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
			//
			// https://learn.microsoft.com/en-us/windows/apps/winui/winui3/localize-winui3-app
			// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/mrtcore/localize-strings
			// https://learn.microsoft.com/ja-jp/windows/apps/winui/winui3/localize-winui3-app
			// https://learn.microsoft.com/ja-jp/windows/apps/windows-app-sdk/mrtcore/localize-strings
			// https://nicksnettravels.builttoroam.com/mrtcore-unpackaged/
			// （非パッケージ化のWinUI3アプリケーションは、手動でPRIファイルを生成する必要がある様子：2023/11/17）
			// https://tera1707.com/entry/2023/07/31/225012
			// （グローバリゼーション）
			// https://learn.microsoft.com/ja-jp/windows/apps/design/globalizing/globalizing-portal
			//
			// https://learn.microsoft.com/ja-jp/windows/uwp/app-resources/localize-strings-ui-manifest#localize-the-string-resources
			// https://learn.microsoft.com/ja-jp/windows/uwp/app-resources/makepri-exe-configuration
			//（実装サンプル）
			// https://xamlbrewer.wordpress.com/category/windows-app-sdk/
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
		{
			string pFilepath = "";
			var pArgs = Environment.GetCommandLineArgs();

			if (pArgs.Length > 1)
			{
				pFilepath = pArgs[1];
			}
			m_pProfile = new Profile(pFilepath);
			m_pDbParams = new DbParams();
			m_pProfile.Load(ref m_pDbParams);

			var pContexts = new SortedDictionary<string, VSQLContext>();
			pContexts["Postgres"] = new SQLContext();
			pContexts["SQLite"] = new SQLiteContext();


			//　データベースインスタンスに接続
			if (m_pDbParams.Validate() == true)
			{
				//　データベース処理インスタンスを選択
				if (m_pDbParams.DriverName.Equals("Postgres") == true)
				{
					m_pSQLContext = new SQLContext(m_pDbParams.HostName, m_pDbParams.InstanceName, m_pDbParams.SchemaName, m_pDbParams.ClientKey, m_pDbParams.ClientCrt, m_pDbParams.TrustCrt);
				}
				else if (m_pDbParams.DriverName.Equals("SQLite") == true)
				{
					m_pSQLContext = new SQLiteContext(m_pDbParams.HostName, m_pDbParams.InstanceName, m_pDbParams.SchemaName, m_pDbParams.ClientKey, m_pDbParams.ClientCrt, m_pDbParams.TrustCrt);
				}
				else
				{
					m_pSQLContext = null;
				}
				pContexts[m_pDbParams.DriverName] = m_pSQLContext;

				m_pCertsStock = Authority.Instance;
				m_pCertsStock.Load(m_pSQLContext, m_pDbParams.IdentityName);
			}
			m_pPrepareFlags = new PrepareFlags();
			m_pPrepareFlags.Check(m_pSQLContext, m_pDbParams, m_pCertsStock);

			m_pWindow = new MainWindow();
			m_pWindow.Title = "RoughCA [" + m_pDbParams.IdentityName + "]";
			m_pWindow.Activate();
		}

		//　組織プロファイルを保存
		public void SaveOrgProfile(DbParams pDbParams)
		{
			if (m_pSQLContext != null)
			{
				m_pSQLContext.SaveOrgProfile(m_pCertsStock.m_pOrgProfile);
//				m_pCertsStock.m_pOrgProfile.Save(m_pSQLContext);
			}

			//　
			m_pProfile.Save(pDbParams);
			
		}

		public VSQLContext	GetSQLContext()
		{
			return (m_pSQLContext);
		}

		protected VSQLContext	m_pSQLContext;

		public Window m_pWindow;
		public Profile m_pProfile;
		public DbParams m_pDbParams;
		public PrepareFlags m_pPrepareFlags;	//　前提条件検査結果（）
		public Authority m_pCertsStock;
	}
}
