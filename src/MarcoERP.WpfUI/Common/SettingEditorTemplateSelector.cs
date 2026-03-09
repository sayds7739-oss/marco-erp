using System.Windows;
using System.Windows.Controls;
using MarcoERP.Application.DTOs.Settings;

namespace MarcoERP.WpfUI.Common
{
    /// <summary>
    /// Selects the appropriate editor template based on the setting's DataType.
    /// bool → ToggleButton, int/decimal → numeric TextBox, string → standard TextBox.
    /// </summary>
    public sealed class SettingEditorTemplateSelector : DataTemplateSelector
    {
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate BoolTemplate { get; set; }
        public DataTemplate NumericTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is SystemSettingDto dto)
            {
                return dto.DataType?.ToLowerInvariant() switch
                {
                    "bool" => BoolTemplate,
                    "int" or "decimal" => NumericTemplate,
                    _ => StringTemplate
                };
            }

            return StringTemplate;
        }
    }
}
