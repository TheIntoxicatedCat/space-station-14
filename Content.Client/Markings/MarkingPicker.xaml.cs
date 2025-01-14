using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Content.Client.CharacterAppearance;
using Content.Client.Stylesheets;
using Content.Shared.CharacterAppearance;
using Content.Shared.Markings;
using Content.Shared.Species;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Markings
{
    [GenerateTypedNameReferences]
    public sealed partial class MarkingPicker : Control
    {
        [Dependency] private readonly MarkingManager _markingManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public Action<MarkingsSet>? OnMarkingAdded;
        public Action<MarkingsSet>? OnMarkingRemoved;
        public Action<MarkingsSet>? OnMarkingColorChange;
        public Action<MarkingsSet>? OnMarkingRankChange;

        private List<Color> _currentMarkingColors = new();

        private Dictionary<MarkingCategories, MarkingPoints> PointLimits = new();
        private Dictionary<MarkingCategories, MarkingPoints> PointsUsed = new();

        private ItemList.Item? _selectedMarking;
        private ItemList.Item? _selectedUnusedMarking;
        private MarkingCategories _selectedMarkingCategory = MarkingCategories.Chest;

        private MarkingsSet _currentMarkings = new();

        private List<MarkingCategories> _markingCategories = Enum.GetValues<MarkingCategories>().ToList();

        private string _currentSpecies = SpeciesManager.DefaultSpecies;

        public void SetData(MarkingsSet newMarkings, string species)
        {
            _currentMarkings = newMarkings;
            _currentSpecies = species;

            // Should marking limits be dependent on species prototypes,
            // or should it be dependent on the entity the
            // species contains? Having marking points as a part of
            // the component allows for any arbitrary thing to have
            // a marking (at this point, it's practically a sprite decoration),
            // but having it as a part of a species makes markings instead
            // be dependent on humanoid variants for constraints
            SpeciesPrototype speciesPrototype = _prototypeManager.Index<SpeciesPrototype>(species);
            EntityPrototype body = _prototypeManager.Index<EntityPrototype>(speciesPrototype.Prototype);

            body.TryGetComponent("Markings", out MarkingsComponent? markingsComponent);

            PointLimits = markingsComponent!.LayerPoints;
            PointsUsed = MarkingPoints.CloneMarkingPointDictionary(PointLimits);

            Populate();
            PopulateUsed();
        }

        public MarkingPicker()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            for (int i = 0; i < _markingCategories.Count; i++)
            {
                CMarkingCategoryButton.AddItem(Loc.GetString($"markings-category-{_markingCategories[i].ToString()}"), i);
            }
            CMarkingCategoryButton.SelectId(_markingCategories.IndexOf(MarkingCategories.Chest));
            CMarkingCategoryButton.OnItemSelected +=  OnCategoryChange;
            CMarkingsUnused.OnItemSelected += item =>
               _selectedUnusedMarking = CMarkingsUnused[item.ItemIndex];

            CMarkingAdd.OnPressed += args =>
                MarkingAdd();

            CMarkingsUsed.OnItemSelected += OnUsedMarkingSelected;

            CMarkingRemove.OnPressed += args =>
                MarkingRemove();

            CMarkingRankUp.OnPressed += _ => SwapMarkingUp();
            CMarkingRankDown.OnPressed += _ => SwapMarkingDown();
        }

        private string GetMarkingName(MarkingPrototype marking) => Loc.GetString($"marking-{marking.ID}");
        private List<string> GetMarkingStateNames(MarkingPrototype marking)
        {
            List<string> result = new();
            foreach (var markingState in marking.Sprites)
            {
                switch (markingState)
                {
                    case SpriteSpecifier.Rsi rsi:
                        result.Add(Loc.GetString($"marking-{marking.ID}-{rsi.RsiState}"));
                        break;
                    case SpriteSpecifier.Texture texture:
                        result.Add(Loc.GetString($"marking-{marking.ID}-{texture.TexturePath.Filename}"));
                        break;
                }
            }

            return result;
        }

        public void Populate()
        {
            CMarkingsUnused.Clear();
            _selectedUnusedMarking = null;

            var markings = _markingManager.CategorizedMarkings();
            foreach (var marking in markings[_selectedMarkingCategory])
            {
                if (_currentMarkings.Contains(marking.AsMarking())) continue;
                if (marking.SpeciesRestrictions != null && !marking.SpeciesRestrictions.Contains(_currentSpecies)) continue;
                var item = CMarkingsUnused.AddItem($"{GetMarkingName(marking)}", marking.Sprites[0].Frame0());
                item.Metadata = marking;
            }

            if (PointsUsed.ContainsKey(_selectedMarkingCategory))
            {
                CMarkingPoints.Visible = true;
            }
            else
            {
                CMarkingPoints.Visible = false;
            }
        }

        // Populate the used marking list. Returns a list of markings that weren't
        // valid to add to the marking list.
        public void PopulateUsed()
        {
            CMarkingsUsed.Clear();
            CMarkingColors.Visible = false;
            _selectedMarking = null;

            // a little slower than the original process
            // (the original method here had the logic
            // tied with the presentation, which did it all
            // in one go including display)
            //
            // BUT
            //
            // it's all client side
            // so does it really matter???
            //
            // actual validation/filtering occurs server side, but
            // it might be better to just have a Process function
            // that just iterates through all the markings with
            // a species and points dict to ensure that all markings
            // that were given are valid?
            //
            // one of the larger issues is that this doesn't
            // necessarily use the existing backing list, but rather it
            // allocates entirely new lists instead to perform
            // their functions, making a 'Process' function
            // more desirable imo, since this isn't *really* used
            // outside of this specific niche

            var markings = new MarkingsSet(_currentMarkings);

            // ensures all markings are valid
            markings = MarkingsSet.EnsureValid(_currentMarkings, _markingManager);

            // filters out all non-valid species markings
            markings = MarkingsSet.FilterSpecies(_currentMarkings, _currentSpecies);

            // processes all the points currently available
            markings = MarkingsSet.ProcessPoints(_currentMarkings, PointsUsed);

            // if the marking set has changed, invoke the event that involves changed marking sets
            if (markings != _currentMarkings)
            {
                Logger.DebugS("Markings", "Marking set is different, resetting markings on dummy now");
                _currentMarkings = markings;
                OnMarkingRemoved?.Invoke(_currentMarkings);
            }

            IEnumerator markingEnumerator = _currentMarkings.GetReverseEnumerator();

            // walk backwards through the list for visual purposes
            while (markingEnumerator.MoveNext())
            {
                Marking marking = (Marking) markingEnumerator.Current;
                var newMarking = _markingManager.Markings()[marking.MarkingId];
                var _item = new ItemList.Item(CMarkingsUsed)
                {
                    Text = Loc.GetString("marking-used", ("marking-name", $"{GetMarkingName(newMarking)}"), ("marking-category", Loc.GetString($"markings-category-{newMarking.MarkingCategory}"))),
                    Icon = newMarking.Sprites[0].Frame0(),
                    Selectable = true,
                    Metadata = newMarking,
                    IconModulate = marking.MarkingColors[0]
                };
                CMarkingsUsed.Add(_item);
            }

            // since all the points have been processed, update the points visually
            UpdatePoints();
        }

        private void SwapMarkingUp()
        {
            if (_selectedMarking == null)
            {
                return;
            }

            var i = CMarkingsUsed.IndexOf(_selectedMarking);
            if (ShiftMarkingRank(i, -1))
            {
                OnMarkingRankChange?.Invoke(_currentMarkings);
            }
        }

        private void SwapMarkingDown()
        {
            if (_selectedMarking == null)
            {
                return;
            }

            var i = CMarkingsUsed.IndexOf(_selectedMarking);
            if (ShiftMarkingRank(i, 1))
            {
                OnMarkingRankChange?.Invoke(_currentMarkings);
            }
        }

        private bool ShiftMarkingRank(int src, int places)
        {
            if (src + places >= CMarkingsUsed.Count || src + places < 0)
            {
                return false;
            }

            var visualDest = src + places; // what it would visually look like
            var visualTemp = CMarkingsUsed[visualDest];
            CMarkingsUsed[visualDest] = CMarkingsUsed[src];
            CMarkingsUsed[src] = visualTemp;

            switch (places)
            {
                // i.e., we're going down in rank
                case < 0:
                    _currentMarkings.ShiftRankDownFromEnd(src);
                    break;
                // i.e., we're going up in rank
                case > 0:
                    _currentMarkings.ShiftRankUpFromEnd(src);
                    break;
                // do nothing?
                default:
                    break;
            }

            return true;
        }

        // repopulate in case markings are restricted,
        // and also filter out any markings that are now invalid
        // attempt to preserve any existing markings as well:
        // it would be frustrating to otherwise have all markings
        // cleared, imo
        public void SetSpecies(string species)
        {
            _currentSpecies = species;
            var markingCount = _currentMarkings.Count;

            SpeciesPrototype speciesPrototype = _prototypeManager.Index<SpeciesPrototype>(species);
            EntityPrototype body = _prototypeManager.Index<EntityPrototype>(speciesPrototype.Prototype);

            body.TryGetComponent("Markings", out MarkingsComponent? markingsComponent);

            PointLimits = markingsComponent!.LayerPoints;
            PointsUsed = MarkingPoints.CloneMarkingPointDictionary(PointLimits);

            Populate();
            PopulateUsed();
        }

        private void UpdatePoints()
        {
            if (PointsUsed.TryGetValue(_selectedMarkingCategory, out var pointsRemaining))
            {
                CMarkingPoints.Text = Loc.GetString("marking-points-remaining", ("points", pointsRemaining.Points));
            }
        }

        private void OnCategoryChange(OptionButton.ItemSelectedEventArgs category)
        {
            CMarkingCategoryButton.SelectId(category.Id);
            _selectedMarkingCategory = _markingCategories[category.Id];
            Populate();
            UpdatePoints();
        }

        // TODO: This should be using ColorSelectorSliders once that's merged, so
        private void OnUsedMarkingSelected(ItemList.ItemListSelectedEventArgs item)
        {
            _selectedMarking = CMarkingsUsed[item.ItemIndex];
            var prototype = (MarkingPrototype) _selectedMarking.Metadata!;

            if (prototype.FollowSkinColor)
            {
                CMarkingColors.Visible = false;

                return;
            }

            var stateNames = GetMarkingStateNames(prototype);
            _currentMarkingColors.Clear();
            CMarkingColors.DisposeAllChildren();
            List<ColorSelectorSliders> colorSliders = new();
            for (int i = 0; i < prototype.Sprites.Count; i++)
            {
                var colorContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                };

                CMarkingColors.AddChild(colorContainer);

                ColorSelectorSliders colorSelector = new ColorSelectorSliders();
                colorSliders.Add(colorSelector);

                colorContainer.AddChild(new Label { Text = $"{stateNames[i]} color:" });
                colorContainer.AddChild(colorSelector);

                var currentColor = new Color(
                    _currentMarkings[_currentMarkings.Count - 1 - item.ItemIndex].MarkingColors[i].RByte,
                    _currentMarkings[_currentMarkings.Count - 1 - item.ItemIndex].MarkingColors[i].GByte,
                    _currentMarkings[_currentMarkings.Count - 1 - item.ItemIndex].MarkingColors[i].BByte
                );
                colorSelector.Color = currentColor;
                _currentMarkingColors.Add(currentColor);
                int colorIndex = _currentMarkingColors.IndexOf(currentColor);

                Action<Color> colorChanged = delegate(Color color)
                {
                    _currentMarkingColors[colorIndex] = colorSelector.Color;

                    ColorChanged(colorIndex);
                };
                colorSelector.OnColorChanged += colorChanged;
            }

            CMarkingColors.Visible = true;
        }

        private void ColorChanged(int colorIndex)
        {
            if (_selectedMarking is null) return;
            var markingPrototype = (MarkingPrototype) _selectedMarking.Metadata!;
            int markingIndex = _currentMarkings.FindIndexOf(markingPrototype.ID);

            if (markingIndex < 0) return;

            _selectedMarking.IconModulate = _currentMarkingColors[colorIndex];
            _currentMarkings[markingIndex].SetColor(colorIndex, _currentMarkingColors[colorIndex]);
            OnMarkingColorChange?.Invoke(_currentMarkings);
        }

        private void MarkingAdd()
        {
            if (_selectedUnusedMarking is null) return;

            MarkingPrototype marking = (MarkingPrototype) _selectedUnusedMarking.Metadata!;

            if (PointsUsed.TryGetValue(marking.MarkingCategory, out var points))
            {
                if (points.Points == 0)
                {
                    return;
                }

                points.Points--;
            }

            UpdatePoints();

            _currentMarkings.AddBack(marking.AsMarking());

            CMarkingsUnused.Remove(_selectedUnusedMarking);
            var item = new ItemList.Item(CMarkingsUsed)
            {
                Text = Loc.GetString("marking-used", ("marking-name", $"{GetMarkingName(marking)}"), ("marking-category", Loc.GetString($"markings-category-{marking.MarkingCategory}"))),
                Icon = marking.Sprites[0].Frame0(),
                Selectable = true,
                Metadata = marking,
            };
            CMarkingsUsed.Insert(0, item);

            _selectedUnusedMarking = null;
            OnMarkingAdded?.Invoke(_currentMarkings);
        }

        private void MarkingRemove()
        {
            if (_selectedMarking is null) return;

            MarkingPrototype marking = (MarkingPrototype) _selectedMarking.Metadata!;

            if (PointsUsed.TryGetValue(marking.MarkingCategory, out var points))
            {
                points.Points++;
            }

            UpdatePoints();

            _currentMarkings.Remove(marking.AsMarking());
            CMarkingsUsed.Remove(_selectedMarking);

            if (marking.MarkingCategory == _selectedMarkingCategory)
            {
                var item = CMarkingsUnused.AddItem($"{GetMarkingName(marking)}", marking.Sprites[0].Frame0());
                item.Metadata = marking;
            }
            _selectedMarking = null;
            CMarkingColors.Visible = false;
            OnMarkingRemoved?.Invoke(_currentMarkings);
        }
    }
}
