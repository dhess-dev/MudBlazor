﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor.Utilities;

namespace MudBlazor
{
    /// <summary>
    /// Represents a component with simple and flexible type-ahead functionality.
    /// </summary>
    /// <typeparam name="T">The type of item to search.</typeparam>
    public partial class MudAutocomplete<T> : MudBaseInput<T>
    {
        /// <summary>
        /// We need a random id for the year items in the year list so we can scroll to the item safely in every DatePicker.
        /// </summary>
        private readonly string _componentId = Guid.NewGuid().ToString();

        /// <summary>
        /// This boolean will keep track if the clear function is called too keep the set text function to be called.
        /// </summary>
        private bool _isCleared;
        private bool _isClearing;
        private bool _isProcessingValue;
        private int _selectedListItemIndex = 0;
        private int _elementKey = 0;
        private int _returnedItemsCount;
        private bool _isOpen;
        private MudInput<string> _elementReference;
        private CancellationTokenSource _cancellationTokenSrc;
        private Task _currentSearchTask;
        private Timer _timer;
        private T[] _items;
        private IList<int> _enabledItemIndices = new List<int>();
        private Func<T, string> _toStringFunc;

        [Inject]
        private IScrollManager ScrollManager { get; set; }

        protected string Classname =>
            new CssBuilder("mud-select")
            .AddClass(Class)
            .Build();

        protected string AutocompleteClassname =>
            new CssBuilder("mud-select")
            .AddClass("mud-autocomplete")
            .AddClass("mud-width-full", FullWidth)
            .AddClass("mud-autocomplete--with-progress", ShowProgressIndicator && IsLoading)
            .Build();

        protected string CircularProgressClassname =>
            new CssBuilder("progress-indicator-circular")
            .AddClass("progress-indicator-circular--with-adornment", Adornment == Adornment.End)
            .Build();

        protected string GetListItemClassname(bool isSelected) =>
            new CssBuilder()
            .AddClass("mud-selected-item mud-primary-text mud-primary-hover", isSelected)
            .AddClass(ListItemClass)
            .Build();

        /// <summary>
        /// Gets or sets the CSS classes applied to the popover.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  You can use spaces to separate multiple classes.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public string PopoverClass { get; set; }

        /// <summary>
        /// Gets or sets the CSS classes applied to the internal list.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  You can use spaces to separate multiple classes.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public string ListClass { get; set; }

        /// <summary>
        /// Gets or sets the CSS classes applied to internal list items.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  You can use spaces to separate multiple classes.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public string ListItemClass { get; set; }

        /// <summary>
        /// Gets or sets where the popover will open from.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Origin.BottomCenter" />.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public Origin AnchorOrigin { get; set; } = Origin.BottomCenter;

        /// <summary>
        /// Gets or sets the transform origin point for the popover.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Origin.TopCenter"/>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public Origin TransformOrigin { get; set; } = Origin.TopCenter;

        /// <summary>
        /// Gets or sets whether compact padding will be used.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public bool Dense { get; set; }

        /// <summary>
        /// Gets or sets the "open" Autocomplete icon.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Icons.Material.Filled.ArrowDropDown"/>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Appearance)]
        public string OpenIcon { get; set; } = Icons.Material.Filled.ArrowDropDown;

        /// <summary>
        /// Gets or sets the "close" Autocomplete icon.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Icons.Material.Filled.ArrowDropDown"/>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Appearance)]
        public string CloseIcon { get; set; } = Icons.Material.Filled.ArrowDropUp;

        /// <summary>
        /// Gets or sets the maximum height, in pixels, of the Autocomplete when it is open.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>300</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListAppearance)]
        public int MaxHeight { get; set; } = 300;

        /// <summary>
        /// Gets or sets the function used to get the display text for each item.
        /// </summary>
        /// <remarks>
        /// Defaults to the <c>ToString()</c> method of items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public Func<T, string> ToStringFunc
        {
            get => _toStringFunc;
            set
            {
                if (_toStringFunc == value)
                    return;
                _toStringFunc = value;
                Converter = new Converter<T>
                {
                    SetFunc = _toStringFunc ?? (x => x?.ToString()),
                };
            }
        }

        /// <summary>
        /// Gets or sets whether to show the progress indicator during searches. 
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  The progress indicator uses the color specified in the <see cref="ProgressIndicatorColor"/> property.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool ShowProgressIndicator { get; set; } = false;

        /// <summary>
        /// Gets or sets the color of the progress indicator.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="Color.Default"/>.  This property is used when <see cref="ShowProgressIndicator"/> is <c>true</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Appearance)]
        public Color ProgressIndicatorColor { get; set; } = Color.Default;

        /// <summary>
        /// Gets or sets a function used to search for items.
        /// </summary>
        /// <remarks>
        /// This function searches for items containing the specified <c>string</c> value, and returns items which match up to the <see cref="MaxItems"/> property.  You can use the provided <see cref="CancellationToken"/> which is marked as canceled when the user changes the search text or selects a value from the list.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public Func<string, CancellationToken, Task<IEnumerable<T>>> SearchFunc { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items to display.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>10</c>.  A value of <c>null</c> will display all items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public int? MaxItems { get; set; } = 10;

        /// <summary>
        /// Gets or sets the minimum number of characters typed to initiate a search.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>0</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public int MinCharacters { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to reset the selected value if the user deletes the text.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool ResetValueOnEmptyText { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the text will be selected (highlighted) when the Autocomplete is clicked.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool SelectOnClick { get; set; } = true;

        /// <summary>
        /// Gets or sets whether other items can be selected without resetting the Value.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c>.  When <c>true</c>, selecting an option will trigger a <see cref="SearchFunc"/> with the current Text.  Otherwise, an empty string is passed which can make it easier to view and select other options without resetting the Value. When <c>false</c>, <c>T</c> must either be a <c>record</c> or override the <c>GetHashCode</c> and <c>Equals</c> methods.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool Strict { get; set; } = true;

        /// <summary>
        /// Gets or sets the debounce interval, in milliseconds.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>100</c>.  A higher value can help reduce the number of calls to <see cref="SearchFunc"/>, which can improve responsiveness.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public int DebounceInterval { get; set; } = 100;

        /// <summary>
        /// Gets or sets the custom template used to display unselected items.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="ItemSelectedTemplate"/> property to control the display of selected items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment<T> ItemTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used to display selected items.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="ItemTemplate"/> property to control the display of unselected items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment<T> ItemSelectedTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used to display disabled items.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment<T> ItemDisabledTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used when the number of items returned by <see cref="SearchFunc"/> is more than the value of the <see cref="MaxItems"/> property.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment MoreItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used when no items are returned by <see cref="SearchFunc"/>.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment NoItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template shown above the list of items, if <see cref="SearchFunc"/> returns items to display.  Otherwise, the fragment is hidden.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="AfterItemsTemplate"/> property to control content displayed below items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment BeforeItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template shown below the list of items, if <see cref="SearchFunc"/> returns items to display.  Otherwise, the fragment is hidden.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="BeforeItemsTemplate"/> property to control content displayed above items.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment AfterItemsTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used for the progress indicator when <see cref="ShowProgressIndicator"/> is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="ProgressIndicatorInPopoverTemplate"/> property to control content displayed for the progress indicator inside the popover.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment ProgressIndicatorTemplate { get; set; }

        /// <summary>
        /// Gets or sets the custom template used for the progress indicator inside the popover when <see cref="ShowProgressIndicator"/> is <c>true</c>.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="ProgressIndicatorTemplate"/> property to control content displayed for the progress indicator.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public RenderFragment ProgressIndicatorInPopoverTemplate { get; set; }

        /// <summary>
        /// Gets or sets whether the <c>Text</c> property is overridden when an item is selected.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c>.  When <c>true</c>, selecting a value will update the Text property.  When <c>false</c>, incomplete values for Text are allowed.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool CoerceText { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the <c>Value</c> property is set even if no match is found by <see cref="SearchFunc"/>.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  When <c>true</c>, the user input will be applied to the Value property which allows it to be validated and show an error message. 
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool CoerceValue { get; set; }

        /// <summary>
        /// Gets or sets the function used to determine if an item should be disabled.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public Func<T, bool> ItemDisabledFunc { get; set; }

        /// <summary>
        /// Occurs when the <see cref="IsOpen"/> property has changed.
        /// </summary>
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }

        /// <summary>
        /// Gets or sets whether pressing the <c>Tab</c> key updates the Value to the currently selected item.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.ListBehavior)]
        public bool SelectValueOnTab { get; set; } = false;

        /// <summary>
        /// Gets or sets whether a Clear icon button is displayed.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>false</c>.  When <c>true</c>, an icon is displayed which, when clicked, clears the Text and Value.  Use the <c>ClearIcon</c> property to control the Clear button icon.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.FormComponent.Behavior)]
        public bool Clearable { get; set; } = false;

        /// <summary>
        /// Occurs when the Clear button has been clicked.
        /// </summary>
        /// <remarks>
        /// The Text and Value properties will be blank when this callback occurs.
        /// </remarks>
        [Parameter]
        public EventCallback<MouseEventArgs> OnClearButtonClick { get; set; }

        /// <summary>
        /// Occurs when the number of items returned by <see cref="SearchFunc"/> has changed.
        /// </summary>
        /// <remarks>
        /// The number of items returned determines when custom templates are shown.  If the number is <c>0</c>, <see cref="NoItemsTemplate"/> will be shown. If the number is beyond <see cref="MaxItems"/>, <see cref="MoreItemsTemplate"/> will be shown.
        /// </remarks>
        [Parameter]
        public EventCallback<int> ReturnedItemsCountChanged { get; set; }

        /// <summary>
        /// Gets or sets whether the search result drop-down is currently displayed.
        /// </summary>
        /// <remarks>
        /// When this property changes, the <see cref="IsOpenChanged"/> event will occur.
        /// </remarks>
        public bool IsOpen
        {
            get => _isOpen;
            // Note: the setter is protected because it was needed by a user who derived his own autocomplete from this class.
            // Note: setting IsOpen will not open or close it. Use ToggleMenu() for that.
            protected set
            {
                if (_isOpen == value)
                    return;
                _isOpen = value;

                IsOpenChanged.InvokeAsync(_isOpen).AndForget();
            }
        }

        private bool IsLoading => _currentSearchTask is { IsCompleted: false };

        private string CurrentIcon => !string.IsNullOrWhiteSpace(AdornmentIcon) ? AdornmentIcon : _isOpen ? CloseIcon : OpenIcon;

        public MudAutocomplete()
        {
            Adornment = Adornment.End;
            IconSize = Size.Medium;
        }

        /// <summary>
        /// Changes the currently selected item to the specified value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="Task"/> object.</returns>
        public async Task SelectOption(T value)
        {
            _isProcessingValue = true;
            try
            {
                await SetValueAsync(value);
                if (_items != null)
                    _selectedListItemIndex = Array.IndexOf(_items, value);
                var optionText = GetItemString(value);
                if (!_isCleared)
                    await SetTextAsync(optionText, false);
                _timer?.Dispose();
                IsOpen = false;
                await BeginValidateAsync();
                if (!_isCleared)
                    _elementReference?.SetText(optionText);
                _elementReference?.FocusAsync().AndForget();
                StateHasChanged();
            }
            finally
            {
                _isProcessingValue = false;
            }
        }

        /// <summary>
        /// Opens or closes the drop-down of items.
        /// </summary>
        /// <remarks>
        /// If the Autocomplete is currently disabled or read-only, it will not be opened.
        /// </remarks>
        public async Task ToggleMenu()
        {
            if ((GetDisabledState() || GetReadOnlyState()) && !IsOpen)
                return;
            await ChangeMenu(!IsOpen);
        }

        private async Task ChangeMenu(bool open)
        {
            if (open)
            {
                if (SelectOnClick)
                    await _elementReference.SelectAsync();
                await OnSearchAsync();
            }
            else
            {
                _timer?.Dispose();
                await RestoreScrollPositionAsync();
                await CoerceTextToValue();
                IsOpen = false;
                StateHasChanged();
            }
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            var text = GetItemString(Value);
            if (!string.IsNullOrWhiteSpace(text))
                Text = text;
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (_isClearing || _isProcessingValue)
            {
                //When you select a value in the popover, SelectOption will be called.
                //When it reaches SetValueAsync, it will be awaited.
                //Meanwhile, in parallel, the Clear method will be called, which sets isCleared to true.
                //However, by the time SetValueAsync is released and SelectOption continues its execution, an OnAfterRender event might fire, setting isCleared back to false.
                //This can result in a race condition.
                //https://github.com/MudBlazor/MudBlazor/pull/6701
                base.OnAfterRender(firstRender);
                return;
            }
            _isCleared = false;
            base.OnAfterRender(firstRender);
        }

        protected override Task UpdateTextPropertyAsync(bool updateValue)
        {
            _timer?.Dispose();
            // This keeps the text from being set when clear() was called
            if (_isCleared)
                return Task.CompletedTask;
            return base.UpdateTextPropertyAsync(updateValue);
        }

        protected override async Task UpdateValuePropertyAsync(bool updateText)
        {
            _timer?.Dispose();
            if (ResetValueOnEmptyText && string.IsNullOrWhiteSpace(Text))
                await SetValueAsync(default(T), updateText);
            if (DebounceInterval <= 0)
                await OnSearchAsync();
            else
                _timer = new Timer(OnTimerComplete, null, DebounceInterval, Timeout.Infinite);
        }

        private void OnTimerComplete(object stateInfo) => InvokeAsync(OnSearchAsync);

        private void CancelToken()
        {
            try
            {
                _cancellationTokenSrc?.Cancel();
            }
            catch { /*ignored*/ }
            finally
            {
                _cancellationTokenSrc = new CancellationTokenSource();
            }
        }

        private Task SetReturnedItemsCountAsync(int value)
        {
            _returnedItemsCount = value;
            return ReturnedItemsCountChanged.InvokeAsync(value);
        }

        /// <remarks>
        /// This async method needs to return a task and be awaited in order for
        /// unit tests that trigger this method to work correctly.
        /// </remarks>
        private async Task OnSearchAsync()
        {
            if (MinCharacters > 0 && (string.IsNullOrWhiteSpace(Text) || Text.Length < MinCharacters))
            {
                IsOpen = false;
                StateHasChanged();
                return;
            }

            var searchedItems = Array.Empty<T>();
            CancelToken();

            var searchingWhileSelected = false;
            try
            {
                if (ProgressIndicatorInPopoverTemplate != null)
                {
                    IsOpen = true;
                }

                searchingWhileSelected = !Strict && Value != null && (Value.ToString() == Text || (ToStringFunc != null && ToStringFunc(Value) == Text)); //search while selected if enabled and the Text is equivalent to the Value
                var searchText = searchingWhileSelected ? string.Empty : Text;

                var searchTask = SearchFunc(searchText, _cancellationTokenSrc.Token);

                _currentSearchTask = searchTask;

                StateHasChanged();
                var searchItems = await searchTask ?? Enumerable.Empty<T>();
                searchedItems = searchItems.ToArray();
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Logger.LogWarning("The search function failed to return results: " + e.Message);
            }

            await SetReturnedItemsCountAsync(searchedItems.Length);
            if (MaxItems.HasValue)
            {
                searchedItems = searchedItems.Take(MaxItems.Value).ToArray();
            }
            _items = searchedItems;

            var enabledItems = _items.Select((item, idx) => (item, idx)).Where(tuple => ItemDisabledFunc?.Invoke(tuple.item) != true).ToList();
            _enabledItemIndices = enabledItems.Select(tuple => tuple.idx).ToList();
            if (searchingWhileSelected) //compute the index of the currently select value, if it exists
            {
                _selectedListItemIndex = Array.IndexOf(_items, Value);
            }
            else
            {
                _selectedListItemIndex = _enabledItemIndices.Any() ? _enabledItemIndices.First() : -1;
            }

            IsOpen = true;

            if (_items?.Length == 0)
            {
                await CoerceValueToText();
                StateHasChanged();
                return;
            }

            if (!CoerceText && CoerceValue)
            {
                await CoerceValueToText();
            }

            StateHasChanged();
        }

        /// <summary>
        /// Resets the Text and Value, and closes the drop-down if it is open.
        /// </summary>
        public async Task Clear()
        {
            _isClearing = true;
            try
            {
                _isCleared = true;
                IsOpen = false;
                await SetTextAsync(null, updateValue: false);
                await CoerceValueToText();
                if (_elementReference != null)
                    await _elementReference.SetText("");
                _timer?.Dispose();
                StateHasChanged();
            }
            finally
            {
                _isClearing = false;
            }
        }

        protected override Task ResetValueAsync() => Clear();

        private string GetItemString(T item)
        {
            if (item == null)
                return string.Empty;
            try
            {
                return Converter.Set(item);
            }
            catch (NullReferenceException) { }
            return "null";
        }

        internal virtual async Task OnInputKeyDown(KeyboardEventArgs args)
        {
            switch (args.Key)
            {
                case "Tab":
                    // NOTE: We need to catch Tab in Keydown because a tab will move focus to the next element and thus
                    // in OnInputKeyUp we'd never get the tab key
                    if (!IsOpen)
                        return;
                    if (SelectValueOnTab)
                        await OnEnterKey();
                    else
                        IsOpen = false;
                    break;
            }
            await base.InvokeKeyDownAsync(args);
        }

        internal virtual async Task OnInputKeyUp(KeyboardEventArgs args)
        {
            switch (args.Key)
            {
                case "Enter":
                case "NumpadEnter":
                    if (!IsOpen)
                    {
                        await ToggleMenu();
                    }
                    else
                    {
                        await OnEnterKey();
                    }
                    break;
                case "ArrowDown":
                    if (!IsOpen)
                    {
                        await ToggleMenu();
                    }
                    else
                    {
                        var increment = _enabledItemIndices.ElementAtOrDefault(_enabledItemIndices.IndexOf(_selectedListItemIndex) + 1) - _selectedListItemIndex;
                        await SelectNextItem(increment < 0 ? 1 : increment);
                    }
                    break;
                case "ArrowUp":
                    if (args.AltKey)
                    {
                        await ChangeMenu(open: false);
                    }
                    else if (!IsOpen)
                    {
                        await ToggleMenu();
                    }
                    else
                    {
                        var decrement = _selectedListItemIndex - _enabledItemIndices.ElementAtOrDefault(_enabledItemIndices.IndexOf(_selectedListItemIndex) - 1);
                        await SelectNextItem(-(decrement < 0 ? 1 : decrement));
                    }
                    break;
                case "Escape":
                    await ChangeMenu(open: false);
                    break;
                case "Tab":
                    await Task.Delay(1);
                    if (!IsOpen)
                        return;
                    if (SelectValueOnTab)
                        await OnEnterKey();
                    else
                        await ToggleMenu();
                    break;
                case "Backspace":
                    if (args.CtrlKey && args.ShiftKey)
                    {
                        await ResetAsync();
                    }
                    break;
            }
            await base.InvokeKeyUpAsync(args);
        }

        private ValueTask SelectNextItem(int increment)
        {
            if (increment == 0 || _items == null || _items.Length == 0 || !_enabledItemIndices.Any())
                return ValueTask.CompletedTask;
            // if we are at the end, or the beginning we just do an rollover
            _selectedListItemIndex = Math.Clamp(value: ((10 * _items.Length) + _selectedListItemIndex + increment) % _items.Length, min: 0, max: _items.Length - 1);
            return ScrollToListItem(_selectedListItemIndex);
        }

        /// <summary>
        /// Scrolls the list of items to the item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to scroll to.</param>
        public ValueTask ScrollToListItem(int index)
        {
            var id = GetListItemId(index);
            //id of the scrolled element
            return ScrollManager.ScrollToListItemAsync(id);
        }

        //This restores the scroll position after closing the menu and element being 0
        private ValueTask RestoreScrollPositionAsync()
        {
            if (_selectedListItemIndex != 0) return ValueTask.CompletedTask;
            return ScrollManager.ScrollToListItemAsync(GetListItemId(0));
        }

        private string GetListItemId(in int index)
        {
            return $"{_componentId}_item{index}";
        }

        internal async Task OnEnterKey()
        {
            if (IsOpen == false)
                return;
            try
            {
                if (_items == null || _items.Length == 0)
                    return;
                if (_selectedListItemIndex >= 0 && _selectedListItemIndex < _items.Length)
                    await SelectOption(_items[_selectedListItemIndex]);
            }
            finally
            {
                if (IsOpen)
                    IsOpen = false;
            }
        }

        private Task OnInputBlurred(FocusEventArgs args)
        {
            return OnBlur.InvokeAsync(args);
            // we should not validate on blur in autocomplete, because the user needs to click out of the input to select a value,
            // resulting in a premature validation. thus, don't call base
            //base.OnBlurred(args);
        }

        private Task CoerceTextToValue()
        {
            if (CoerceText == false)
                return Task.CompletedTask;

            _timer?.Dispose();

            var text = Value == null ? null : GetItemString(Value);

            // Don't update the value to prevent the popover from opening again after coercion
            if (text != Text)
                return SetTextAsync(text, updateValue: false);

            return Task.CompletedTask;
        }

        private Task CoerceValueToText()
        {
            if (CoerceValue == false)
                return Task.CompletedTask;
            _timer?.Dispose();
            var value = Converter.Get(Text);
            return SetValueAsync(value, updateText: false);
        }

        protected override void Dispose(bool disposing)
        {
            _timer?.Dispose();

            if (_cancellationTokenSrc != null)
            {
                try
                {
                    _cancellationTokenSrc.Dispose();
                }
                catch { /*ignored*/ }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Sets focus to this Autocomplete.
        /// </summary>
        public override ValueTask FocusAsync()
        {
            return _elementReference.FocusAsync();
        }

        /// <summary>
        /// Releases focus from this Autocomplete.
        /// </summary>
        public override ValueTask BlurAsync()
        {
            return _elementReference.BlurAsync();
        }

        /// <summary>
        /// Selects all of the current text within the Autocomplete text box.
        /// </summary>
        public override ValueTask SelectAsync()
        {
            return _elementReference.SelectAsync();
        }

        /// <summary>
        /// Selects a portion of the text within the Autocomplete text box.
        /// </summary>
        /// <param name="pos1">The index of the first character to select.</param>
        /// <param name="pos2">The index of the last character to select.</param>
        /// <returns>A <see cref="ValueTask"/> object.</returns>
        public override ValueTask SelectRangeAsync(int pos1, int pos2)
        {
            return _elementReference.SelectRangeAsync(pos1, pos2);
        }

        private async Task OnTextChanged(string text)
        {
            await base.TextChanged.InvokeAsync(text);

            if (text == null)
                return;
            await SetTextAsync(text, true);
        }

        private Task ListItemOnClick(T item) => SelectOption(item);
    }
}
