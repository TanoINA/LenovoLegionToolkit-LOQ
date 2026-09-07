using System;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

public interface INotificationWindow
{
    void Show(int closeAfter);
    void Close(bool immediate);
    bool IsOpen { get; }
    ScreenInfo ScreenInfo { get; }
    void Update(SymbolRegular symbol, SymbolRegular? overlaySymbol, Action<SymbolIcon>? symbolTransform, string text, Brush? textColor, Action? clickAction, NotificationPosition position, int closeAfter);
}
