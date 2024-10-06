using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Arteria_s.UI.Base
{
	public static class PickerHelper
	{
		//　相互運用機能を通して、UWP/WPF/Win32API時代の関数を実行
		public static FileOpenPicker NewFileOpenPicker(Window pWindow)
		{
			FileOpenPicker pOpenPicker = new FileOpenPicker();
			var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(pWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(pOpenPicker, hWnd);
			return(pOpenPicker);
		}
	}
}
