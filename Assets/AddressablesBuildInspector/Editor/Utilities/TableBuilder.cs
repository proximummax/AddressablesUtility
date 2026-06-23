using System;
using UnityEngine.UIElements;

namespace AddressablesBuildInspector.Editor.Utilities
{
    /// <summary>
    /// Creates reusable UI Toolkit table fragments for the inspector window.
    /// </summary>
    public static class TableBuilder
    {
        /// <summary>
        /// Creates a horizontal table header with sortable buttons.
        /// </summary>
        /// <param name="columns">Column definitions to render.</param>
        /// <returns>Header visual element.</returns>
        public static VisualElement CreateHeader(params TableColumnDefinition[] columns)
        {
            var header = new VisualElement();
            header.AddToClassList("abi-table-header");

            foreach (TableColumnDefinition column in columns)
            {
                var button = new Button(column.Clicked)
                {
                    text = column.Title,
                    tooltip = column.Tooltip
                };

                button.AddToClassList("abi-header-button");
                button.style.flexGrow = column.FlexGrow;
                header.Add(button);
            }

            return header;
        }

        /// <summary>
        /// Creates a recycled table row container.
        /// </summary>
        /// <returns>Row visual element.</returns>
        public static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.AddToClassList("abi-table-row");
            return row;
        }

        /// <summary>
        /// Creates a label cell.
        /// </summary>
        /// <param name="name">Element name for later binding.</param>
        /// <param name="flexGrow">Relative column width.</param>
        /// <param name="numeric">True when the cell should be right-aligned.</param>
        /// <returns>Label configured as a table cell.</returns>
        public static Label CreateCell(string name, float flexGrow, bool numeric = false)
        {
            var label = new Label
            {
                name = name
            };

            label.AddToClassList("abi-cell");
            if (numeric)
            {
                label.AddToClassList("abi-cell-number");
            }

            label.style.flexGrow = flexGrow;
            return label;
        }

        /// <summary>
        /// Creates a friendly empty-state label.
        /// </summary>
        /// <param name="message">Message to display.</param>
        /// <returns>Empty-state label.</returns>
        public static Label CreateEmptyState(string message)
        {
            var label = new Label(message);
            label.AddToClassList("abi-empty-state");
            return label;
        }
    }

    /// <summary>
    /// Defines one table header column.
    /// </summary>
    public readonly struct TableColumnDefinition
    {
        /// <summary>
        /// Creates a table column definition.
        /// </summary>
        /// <param name="title">Button title.</param>
        /// <param name="flexGrow">Relative column width.</param>
        /// <param name="clicked">Sort callback.</param>
        /// <param name="tooltip">Button tooltip.</param>
        public TableColumnDefinition(string title, float flexGrow, Action clicked, string tooltip = "")
        {
            Title = title;
            FlexGrow = flexGrow;
            Clicked = clicked;
            Tooltip = tooltip;
        }

        /// <summary>
        /// Header title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Relative column width.
        /// </summary>
        public float FlexGrow { get; }

        /// <summary>
        /// Callback invoked when the header is clicked.
        /// </summary>
        public Action Clicked { get; }

        /// <summary>
        /// Header tooltip.
        /// </summary>
        public string Tooltip { get; }
    }
}
