namespace Overdraw.App;

internal sealed class KeyDisplayState
{
    private const long LingerMilliseconds = 900;
    private readonly HashSet<uint> _pressedKeys = [];
    private string _lastDisplayText = string.Empty;
    private string _lingerDisplayText = string.Empty;
    private long _lingerUntilTick;
    private bool _isSuppressedShortcut;

    public bool IsEnabled { get; private set; }

    public string DisplayText
    {
        get
        {
            if (!IsEnabled || _isSuppressedShortcut)
            {
                return string.Empty;
            }

            if (_pressedKeys.Count > 0)
            {
                return _lastDisplayText;
            }

            return Environment.TickCount64 <= _lingerUntilTick
                ? _lingerDisplayText
                : string.Empty;
        }
    }

    public bool IsLingering => IsEnabled && _pressedKeys.Count == 0 && Environment.TickCount64 <= _lingerUntilTick;

    public void Toggle()
    {
        IsEnabled = !IsEnabled;
        if (!IsEnabled)
        {
            Clear();
        }
    }

    public bool HandleKeyDown(uint virtualKey)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (virtualKey == VkSpace || (!IsModifier(virtualKey) && !HasModifierPressed()))
        {
            return false;
        }

        var previousDisplayText = DisplayText;
        var changed = _pressedKeys.Add(virtualKey);
        if (IsOverdrawShortcutPressed())
        {
            _isSuppressedShortcut = true;
            ClearDisplayText();
            return !string.IsNullOrEmpty(previousDisplayText);
        }

        if (_isSuppressedShortcut)
        {
            return !string.IsNullOrEmpty(previousDisplayText);
        }

        UpdateDisplayText();
        return changed && DisplayText != previousDisplayText;
    }

    public bool HandleKeyUp(uint virtualKey)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!_pressedKeys.Contains(virtualKey))
        {
            return false;
        }

        var previousDisplayText = DisplayText;
        var changed = _pressedKeys.Remove(virtualKey);
        if (_isSuppressedShortcut)
        {
            if (!HasModifierPressed())
            {
                _isSuppressedShortcut = false;
                _pressedKeys.Clear();
            }

            ClearDisplayText();
            return false;
        }

        if (_pressedKeys.Count == 0)
        {
            _lingerUntilTick = string.IsNullOrEmpty(_lingerDisplayText)
                ? 0
                : Environment.TickCount64 + LingerMilliseconds;
        }
        else if (HasModifierPressed())
        {
            UpdateDisplayText();
        }
        else
        {
            _pressedKeys.Clear();
            _lingerUntilTick = string.IsNullOrEmpty(_lingerDisplayText)
                ? 0
                : Environment.TickCount64 + LingerMilliseconds;
        }

        return changed && DisplayText != previousDisplayText;
    }

    public void Clear()
    {
        _pressedKeys.Clear();
        _isSuppressedShortcut = false;
        ClearDisplayText();
    }

    private void ClearDisplayText()
    {
        _lastDisplayText = string.Empty;
        _lingerDisplayText = string.Empty;
        _lingerUntilTick = 0;
    }

    private void UpdateDisplayText()
    {
        var parts = new List<string>(5);
        AddModifier(parts, "Ctrl", VkControl, VkLeftControl, VkRightControl);
        AddModifier(parts, "Shift", VkShift, VkLeftShift, VkRightShift);
        AddModifier(parts, "Alt", VkMenu, VkLeftMenu, VkRightMenu);
        AddModifier(parts, "Win", VkLeftWin, VkRightWin);
        if (parts.Count == 0)
        {
            _lastDisplayText = string.Empty;
            _lingerDisplayText = string.Empty;
            _lingerUntilTick = 0;
            return;
        }

        var key = _pressedKeys
            .Where(static key => !IsModifier(key) && key != VkSpace)
            .OrderBy(static key => key)
            .Select(GetKeyName)
            .FirstOrDefault(static name => !string.IsNullOrEmpty(name));

        var hasNonModifierKey = !string.IsNullOrEmpty(key);
        if (!hasNonModifierKey)
        {
            _lastDisplayText = string.Empty;
            _lingerUntilTick = 0;
            return;
        }

        parts.Add(key!);
        _lastDisplayText = string.Join("+", parts);
        _lingerDisplayText = _lastDisplayText;
        _lingerUntilTick = 0;
    }

    private void AddModifier(List<string> parts, string name, params uint[] virtualKeys)
    {
        if (virtualKeys.Any(_pressedKeys.Contains))
        {
            parts.Add(name);
        }
    }

    private bool HasModifierPressed()
    {
        return _pressedKeys.Any(IsModifier);
    }

    private bool IsOverdrawShortcutPressed()
    {
        return HasControlPressed() &&
            HasShiftPressed() &&
            _pressedKeys.Any(static key => key is VkBackspace or VkUp or VkDown or VkC or VkK or VkY or VkZ or VkF12);
    }

    private bool HasControlPressed()
    {
        return _pressedKeys.Contains(VkControl) ||
            _pressedKeys.Contains(VkLeftControl) ||
            _pressedKeys.Contains(VkRightControl);
    }

    private bool HasShiftPressed()
    {
        return _pressedKeys.Contains(VkShift) ||
            _pressedKeys.Contains(VkLeftShift) ||
            _pressedKeys.Contains(VkRightShift);
    }

    private static bool IsModifier(uint virtualKey)
    {
        return virtualKey is VkControl
            or VkLeftControl
            or VkRightControl
            or VkShift
            or VkLeftShift
            or VkRightShift
            or VkMenu
            or VkLeftMenu
            or VkRightMenu
            or VkLeftWin
            or VkRightWin;
    }

    private static string GetKeyName(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= VkF1 and <= VkF24)
        {
            return $"F{virtualKey - VkF1 + 1}";
        }

        return virtualKey switch
        {
            VkBackspace => "Backspace",
            VkTab => "Tab",
            VkEnter => "Enter",
            VkEscape => "Esc",
            VkSpace => "Space",
            VkPageUp => "PageUp",
            VkPageDown => "PageDown",
            VkEnd => "End",
            VkHome => "Home",
            VkLeft => "Left",
            VkUp => "Up",
            VkRight => "Right",
            VkDown => "Down",
            VkInsert => "Insert",
            VkDelete => "Delete",
            _ => string.Empty
        };
    }

    private const uint VkBackspace = 0x08;
    private const uint VkTab = 0x09;
    private const uint VkEnter = 0x0D;
    private const uint VkShift = 0x10;
    private const uint VkControl = 0x11;
    private const uint VkMenu = 0x12;
    private const uint VkEscape = 0x1B;
    private const uint VkSpace = 0x20;
    private const uint VkPageUp = 0x21;
    private const uint VkPageDown = 0x22;
    private const uint VkEnd = 0x23;
    private const uint VkHome = 0x24;
    private const uint VkLeft = 0x25;
    private const uint VkUp = 0x26;
    private const uint VkRight = 0x27;
    private const uint VkDown = 0x28;
    private const uint VkInsert = 0x2D;
    private const uint VkDelete = 0x2E;
    private const uint VkC = 0x43;
    private const uint VkK = 0x4B;
    private const uint VkY = 0x59;
    private const uint VkZ = 0x5A;
    private const uint VkLeftWin = 0x5B;
    private const uint VkRightWin = 0x5C;
    private const uint VkF1 = 0x70;
    private const uint VkF12 = 0x7B;
    private const uint VkF24 = 0x87;
    private const uint VkLeftShift = 0xA0;
    private const uint VkRightShift = 0xA1;
    private const uint VkLeftControl = 0xA2;
    private const uint VkRightControl = 0xA3;
    private const uint VkLeftMenu = 0xA4;
    private const uint VkRightMenu = 0xA5;
}
