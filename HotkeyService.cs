using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScottWisper
{
    // Temporary stub for Task 1 - will be fully implemented in Task 2
    public class HotkeyService : IDisposable
    {
        public event EventHandler? HotkeyPressed;

        public HotkeyService()
        {
            // TODO: Implement hotkey registration in Task 2
            System.Diagnostics.Debug.WriteLine("HotkeyService initialized (stub)");
        }

        public void Dispose()
        {
            // TODO: Implement cleanup in Task 2
            System.Diagnostics.Debug.WriteLine("HotkeyService disposed (stub)");
        }
    }
}