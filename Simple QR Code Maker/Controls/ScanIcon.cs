﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Simple_QR_Code_Maker.Controls;

public partial class ScanIcon : FontIcon
{
	public ScanIcon()
	{
		this.FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"];
		this.Glyph = "\uEE6F";
	}
}
