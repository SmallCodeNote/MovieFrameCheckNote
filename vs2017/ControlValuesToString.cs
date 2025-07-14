using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Collections.Generic;

namespace ControlValuesToStringClass
{
    static class ControlValuesToString
    {
        public static string GetString(Window window)
        {
            var sb = new StringBuilder();
            var visited = new HashSet<DependencyObject>();
            Traverse(window, sb, visited);
            return sb.ToString();
        }

        private static void Traverse(object obj, StringBuilder sb, HashSet<DependencyObject> visited)
        {
            if (obj is DependencyObject depObj)
            {
                if (visited.Contains(depObj)) return;
                visited.Add(depObj);

                switch (obj)
                {
                    case TextBox textBox:
                        string safeText = Uri.EscapeDataString(textBox.Text);
                        sb.AppendLine($"TextBox[{textBox.Name}]: {safeText}");
                        break;

                    case ComboBox comboBox:
                        if (!IsInsideDataGrid(comboBox)) 
                        {
                            var value = comboBox.SelectedValue ?? comboBox.SelectedItem ?? string.Empty;
                            sb.AppendLine($"ComboBox[{comboBox.Name}]: {value}");
                        }
                        break;
                }

                // VisualTree search
                if (depObj is Visual || depObj is Visual3D)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                    {
                        var child = VisualTreeHelper.GetChild(depObj, i);
                        Traverse(child, sb, visited);
                    }
                }

                // LogicalTree search
                foreach (var child in LogicalTreeHelper.GetChildren(depObj))
                {
                    if (child is DependencyObject depChild)
                        Traverse(depChild, sb, visited);
                }

                // TabItem.Content search
                if (depObj is TabItem tabItem && tabItem.Content is DependencyObject tabContent)
                    Traverse(tabContent, sb, visited);
            }
        }

        public static void PutValue(Window window, string ControlValues)
        {
            string[] lines = ControlValues.Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"^(TextBox|ComboBox)\[(.+?)\]: (.*)$");
                if (!match.Success) continue;

                string type = match.Groups[1].Value;
                string name = match.Groups[2].Value;
                string rawValue = match.Groups[3].Value;
                string value = type == "TextBox" ? Uri.UnescapeDataString(rawValue) : rawValue;

                var control = FindControlByName(window, name);
                if (control == null) continue;

                switch (type)
                {
                    case "TextBox":
                        if (control is TextBox tb) tb.Text = value;
                        break;
                    case "ComboBox":
                        if (control is ComboBox cb)
                        {
                            cb.SelectedValue = value;
                            if (cb.SelectedValue == null) cb.SelectedItem = value;
                        }
                        break;
                }
            }
        }

        private static FrameworkElement FindControlByName(DependencyObject parent, string name)
        {
            if (parent is FrameworkElement fe && fe.Name == name)
                return fe;

            if (parent is Visual || parent is Visual3D)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    var result = FindControlByName(child, name);
                    if (result != null) return result;
                }
            }

            foreach (var child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is DependencyObject depChild)
                {
                    var result = FindControlByName(depChild, name);
                    if (result != null) return result;
                }
            }

            return null;
        }

        private static bool IsInsideDataGrid(DependencyObject element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (current is DataGridRow)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

    }
}
