using System;

namespace Arteria_s.App.RoughCA
{
	public enum AppFacility
	{
		Unknown = 0,
		Error,
		Info,
		Complete,
	}

	public enum AppError
	{
		NoError = 0,
		ExistSameCertificate,       //　同じサブジェクト名を持つ証明書が既に登録済み
		FailreSaveCertificate,      //　証明書データの保存に失敗
		FailureCreateCertificate,   //　証明書データの生成に失敗
		InvalidCertificate,         //　無効な証明書を選択

	}

	public enum AppFlow
	{
		Unknown = 0,
		CreateCertificateForClient,
		CreateCertificateForServer,
		CreateCertificateForUpdate,
		Revoke,
	}

	public class AppException : Exception
	{
		public AppError 	m_eError;
		public AppFacility	m_eFacility;
		public AppFlow		m_eFlow;
		public string		Parameter;

		public AppException(AppError eError, AppFacility eFacility, AppFlow eFlow, string pParameter)
		{
			m_eError    = eError;
			m_eFacility = eFacility;
			m_eFlow     = eFlow;
			Parameter   = pParameter;
		}

		public string GetText()
		{
			var pText = "";
			switch (m_eError)
			{
			case AppError.NoError:
				pText = "no error.";
				break;
			case AppError.ExistSameCertificate:
				pText = "同じサブジェクト名を持つ有効な証明書が存在します。";
				break;
			case AppError.FailreSaveCertificate:
				pText = "証明書データの保存に失敗しました。";
				break;
			case AppError.FailureCreateCertificate:
				pText = "証明書データの作成に失敗しました。";
				break;
			case AppError.InvalidCertificate:
				pText = "失効した証明書を選択しています。";
				break;
			}

			return (pText);
		}

		public string GetParameter()
		{
			return (Parameter);
		}
	}
}
