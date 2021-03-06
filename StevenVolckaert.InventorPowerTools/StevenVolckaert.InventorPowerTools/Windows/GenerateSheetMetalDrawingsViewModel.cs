﻿namespace StevenVolckaert.InventorPowerTools.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Inventor;

    internal class GenerateSheetMetalDrawingsViewModel : GenerateDrawingsViewModelBase
    {
        private AssemblyDocument _assembly;
        public AssemblyDocument Assembly
        {
            get { return _assembly; }
            set
            {
                if (_assembly != value)
                {
                    _assembly = value;
                    RaisePropertyChanged(() => Assembly);
                }
            }
        }

        private List<Part> _parts;
        public List<Part> Parts
        {
            get { return _parts; }
            set
            {
                if (_parts != value)
                {
                    _parts = value;
                    RaisePropertyChanged(() => Parts);

                    _documents = value.Cast<IDocument>().ToList();
                    ComputeIsEverythingSelected();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateSheetMetalDrawingsViewModel"/> class.
        /// </summary>
        public GenerateSheetMetalDrawingsViewModel()
        {
            Title = "Generate Sheet Metal Flat Pattern Drawings";
        }

        protected override void GenerateDrawings()
        {
            if (Parts == null)
                return;

            var selectedParts = Parts.Where(x => x.IsSelected == true).ToList();

            if (selectedParts.Count == 0)
            {
                ShowWarningMessageBox("No sheet metal parts are selected.");
                return;
            }

            var bom = Assembly.ComponentDefinition.BOM;

            if (bom != null && bom.RequiresUpdate)
                AddIn.ShowWarningMessageBox(
                    Title,
                    "The BOM of assembly '{0}' requires an update.{1}Quantities displayed in the generated drawings might be incorrect.",
                    Assembly.DisplayName, System.Environment.NewLine + System.Environment.NewLine
                );

            foreach (var part in selectedParts)
            {
                var drawingDocument = CreateDrawingDocument();
                var dimensionStyle = drawingDocument.StylesManager.ActiveStandardStyle.ActiveObjectDefaults.LinearDimensionStyle;

                var sheet = drawingDocument.ActiveSheet;
                var topRightCorner = sheet.TopRightCorner();

                try
                {
                    // 1. Alter formatting of custom properties.
                    SetCustomPropertyFormat(part);

                    // 2. Add flat pattern base view.
                    var flatPatternView = sheet.DrawingViews.AddBaseView(
                        Model: (_Document)part.Document,
                        Position: drawingDocument.ActiveSheet.CenterPoint(),
                        Scale: Scale,
                        ViewOrientation: ViewOrientationTypeEnum.kDefaultViewOrientation,
                        ViewStyle: DrawingViewStyleEnum.kHiddenLineDrawingViewStyle,
                        ModelViewName: string.Empty,
                        ArbitraryCamera: Type.Missing,
                        AdditionalOptions: AddIn.CreateNameValueMap("SheetMetalFoldedModel", false)
                    );

                    flatPatternView.AddHorizontalBendLineDimensionSet(dimensionStyle);
                    flatPatternView.AddVerticalBendLineDimensionSet(dimensionStyle);

                    if (flatPatternView.VerticalLines().Any(x => x.IsBendLine()))
                        flatPatternView.AddHorizontalDimension(dimensionStyle, drawingDistance: 2.0);

                    if (flatPatternView.HorizontalLines().Any(x => x.IsBendLine()))
                        flatPatternView.AddVerticalDimension(dimensionStyle, drawingDistance: 2.0);

                    // 3. Add part list to the top right corner.
                    var partsList = sheet.AddPartsList(part.Document, PartsListLevelEnum.kPartsOnly);
                    var quantity = Assembly.GetPartQuantity(part.Document);

                    if (quantity > 0)
                        partsList.PartsListRows[1]["QTY"].Value = quantity.ToString();

                    // 4. Add base "ISO Top Right", hidden line removed, shaded base view of the part in the drawing's top right corner.
                    var perspectiveView = sheet.DrawingViews.AddBaseView(
                        Model: (_Document)part.Document,
                        Position: drawingDocument.ActiveSheet.TopRightPoint(),
                        Scale: 0.1,
                        ViewOrientation: ViewOrientationTypeEnum.kIsoTopRightViewOrientation,
                        ViewStyle: DrawingViewStyleEnum.kHiddenLineDrawingViewStyle,
                        ModelViewName: string.Empty,
                        ArbitraryCamera: Type.Missing,
                        AdditionalOptions: Type.Missing
                    );

                    var margin = sheet.Margin();

                    perspectiveView.Fit(
                        new Rectangle(
                            AddIn.CreatePoint2D(
                                ((sheet.Width - margin.Right) * 3 + margin.Left) / 4 + 1,
                                ((sheet.Height - margin.Top) * 3 + margin.Bottom) / 4 + 1
                            ),
                            AddIn.CreatePoint2D(
                                topRightCorner.X - 1,
                                topRightCorner.Y - 1
                            )
                        )
                    );

                    perspectiveView.Position =
                        AddIn.CreatePoint2D(
                            perspectiveView.Position.X,
                            perspectiveView.Position.Y - partsList.RangeBox.Height()
                        );

                    // 5. TODO Add 'Top View' below the 'ISO Top Right' view.
                    // TODO Implement extension method 'BottomRightPoint'.
                    var topView = sheet.DrawingViews.AddBaseView(
                        Model: (_Document)part.Document,
                        Position: drawingDocument.ActiveSheet.BottomLeftCorner(),
                        Scale: 0.1,
                        ViewOrientation: ViewOrientationTypeEnum.kTopViewOrientation,
                        ViewStyle: DrawingViewStyleEnum.kHiddenLineDrawingViewStyle,
                        ModelViewName: string.Empty,
                        ArbitraryCamera: Type.Missing,
                        AdditionalOptions: Type.Missing
                    );

                    topView.Position =
                        AddIn.CreatePoint2D(
                            margin.Left + topView.Width + 1,
                            margin.Bottom + topView.Height + 1
                        );
                }
                catch (Exception ex)
                {
                    ShowWarningMessageBox(ex.ToString());
                }
            }
        }
    }
}
