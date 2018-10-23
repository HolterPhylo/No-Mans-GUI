﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.AvalonDock.Layout;

namespace NoMansGUI.Docking
{
    class LayoutInitializer : ILayoutUpdateStrategy
    {
        public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown)
        {
            var tool = anchorableShown.Content as ITool;
            if(tool != null)
            {
                var anchorable = anchorableShown.Parent as LayoutAnchorablePane;
                if(anchorable != null && anchorable.ChildrenCount == 1)
                {
                    switch(tool.PreferredLocation)
                    {
                        case PaneLocation.Left:
                        case PaneLocation.Right:
                            anchorable.DockWidth = new GridLength(tool.PreferredWidth, GridUnitType.Pixel);
                            break;
                        case PaneLocation.Bottom:
                            anchorable.DockHeight = new GridLength(tool.PreferredHeight, GridUnitType.Pixel);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        public void AfterInsertDocument(LayoutRoot layout, LayoutDocument anchorableShown)
        {
          
        }

        public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
        {
            var tool = anchorableToShow.Content as ITool;
            if(tool != null)
            {
                var preferredLocation = tool.PreferredLocation;
                string paneName = GetPaneName(preferredLocation);
                var toolsPane = layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault(d => d.Name == paneName);
                if(toolsPane == null)
                {
                    switch(preferredLocation)
                    {
                        case PaneLocation.Left:
                            toolsPane = CreateAnchorablePane(layout, Orientation.Horizontal, paneName, InsertPosition.Start);
                            break;
                        case PaneLocation.Right:
                            toolsPane = CreateAnchorablePane(layout, Orientation.Horizontal, paneName, InsertPosition.End);
                            break;
                        case PaneLocation.Bottom:
                            toolsPane = CreateAnchorablePane(layout, Orientation.Vertical, paneName, InsertPosition.End);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                toolsPane.Children.Add(anchorableToShow);
                return true;
            }
            return false;
        }

        private static string GetPaneName(PaneLocation location)
        {
            switch(location)
            {
                case PaneLocation.Left:
                    return "LeftPane";
                case PaneLocation.Right:
                    return "RightPane";
                case PaneLocation.Bottom:
                    return "BottomPane";
                default:
                    throw new ArgumentOutOfRangeException("location");
            }
        }

        private static LayoutAnchorablePane CreateAnchorablePane(LayoutRoot layout, Orientation orientation, string paneName, InsertPosition position)
        {
            var parent = layout.Descendents().OfType<LayoutPanel>().First(d => d.Orientation == orientation);
            var toolsPane = new LayoutAnchorablePane { Name = paneName };
            if(position == InsertPosition.Start)
            {
                parent.InsertChildAt(0, toolsPane);
            }
            else
            {
                parent.Children.Add(toolsPane);
            }
            return toolsPane;
        }

        private enum InsertPosition
        {
            Start,
            End
        }

        public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument anchorableToShow, ILayoutContainer destinationContainer)
        {
            return false;
        }
    }
}
