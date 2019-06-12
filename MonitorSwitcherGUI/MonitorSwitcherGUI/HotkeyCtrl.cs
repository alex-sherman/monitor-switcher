using System;
using System.Windows.Forms;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace MonitorSwitcherGUI
{
    public class HotkeyCtrl : IMessageFilter
    {
        #region Interop

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int UnregisterHotKey(IntPtr hWnd, int id);

        private const uint WM_HOTKEY = 0x312;

        private const uint MOD_ALT = 0x1;
        private const uint MOD_CONTROL = 0x2;
        private const uint MOD_SHIFT = 0x4;
        private const uint MOD_WIN = 0x8;

        private const uint ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        #endregion

        private static int currentID;
        private const int maximumID = 0xBFFF;

        private Keys keyCode;
        private bool shift;
        private bool control;
        private bool alt;
        private bool windows;

        [XmlIgnore]
        private int id;
        [XmlIgnore]
        private bool registered = false;
        [XmlIgnore]
        private Control windowControl;

        public event Action<HotkeyCtrl> Pressed;

        public HotkeyCtrl()
        {
            // Register us as a message filter
            Application.AddMessageFilter(this);
        }

        ~HotkeyCtrl()
        {
            Application.RemoveMessageFilter(this);
            // Unregister the hotkey if necessary
            if (this.Registered)
            { this.Unregister(); }
        }

        public bool Register(Control windowControl, Hotkey hotkey)
        {
            // Check that we have not registered
            if (this.registered)
            { throw new NotSupportedException("You cannot register a hotkey that is already registered"); }

            // Get an ID for the hotkey and increase current ID
            this.id = HotkeyCtrl.currentID;
            HotkeyCtrl.currentID = HotkeyCtrl.currentID + 1 % HotkeyCtrl.maximumID;

            // Translate modifier keys into unmanaged version
            uint modifiers = (hotkey.Alt ? MOD_ALT : 0) | (hotkey.Ctrl ? MOD_CONTROL : 0) |
                            (hotkey.Shift ? MOD_SHIFT : 0) /*| (hotkey.Windows ? MOD_WIN : 0)*/;

            // Register the hotkey
            if (HotkeyCtrl.RegisterHotKey(windowControl.Handle, this.id, modifiers, hotkey.Key) == 0)
            {
                // Is the error that the hotkey is registered?
                if (Marshal.GetLastWin32Error() == ERROR_HOTKEY_ALREADY_REGISTERED)
                { return false; }
                else
                { throw new Win32Exception(); }
            }

            // Save the control reference and register state
            this.registered = true;
            this.windowControl = windowControl;

            // We successfully registered
            return true;
        }

        public void Unregister()
        {
            // Check that we have registered
            if (!this.registered)
            { throw new NotSupportedException("You cannot unregister a hotkey that is not registered"); }

            // It's possible that the control itself has died: in that case, no need to unregister!
            if (!this.windowControl.IsDisposed)
            {
                // Clean up after ourselves
                if (HotkeyCtrl.UnregisterHotKey(this.windowControl.Handle, this.id) == 0)
                {
                    //throw new Win32Exception(); 
                }
            }

            // Clear the control reference and register state
            this.registered = false;
            this.windowControl = null;
        }

        public bool PreFilterMessage(ref Message message)
        {
            // Only process WM_HOTKEY messages
            if (message.Msg != HotkeyCtrl.WM_HOTKEY)
            { return false; }

            // Check that the ID is our key and we are registerd
            if (this.registered && (message.WParam.ToInt32() == this.id))
            {
                // Fire the event and pass on the event if our handlers didn't handle it
                return this.OnPressed();
            }
            else
            { return false; }
        }

        private bool OnPressed()
        {
            // Fire the event if we can
            HandledEventArgs handledEventArgs = new HandledEventArgs(false);
            this.Pressed?.Invoke(this);
            // Return whether we handled the event or not
            return true;
        }

        //public override string ToString()
        //{
        //    // We can be empty
        //    if (this.Empty)
        //    { return "(none)"; }

        //    // Build key name
        //    string keyName = Enum.GetName(typeof(Keys), this.keyCode); ;
        //    switch (this.keyCode)
        //    {
        //        case Keys.D0:
        //        case Keys.D1:
        //        case Keys.D2:
        //        case Keys.D3:
        //        case Keys.D4:
        //        case Keys.D5:
        //        case Keys.D6:
        //        case Keys.D7:
        //        case Keys.D8:
        //        case Keys.D9:
        //            // Strip the first character
        //            keyName = keyName.Substring(1);
        //            break;
        //        default:
        //            // Leave everything alone
        //            break;
        //    }

        //    // Build modifiers
        //    string modifiers = "";
        //    if (this.shift)
        //    { modifiers += "Shift+"; }
        //    if (this.control)
        //    { modifiers += "Control+"; }
        //    if (this.alt)
        //    { modifiers += "Alt+"; }
        //    if (this.windows)
        //    { modifiers += "Windows+"; }

        //    // Return result
        //    return modifiers + keyName;
        //}

        public bool Empty
        {
            get { return this.keyCode == Keys.None; }
        }

        public bool Registered
        {
            get { return this.registered; }
        }
    }
}
