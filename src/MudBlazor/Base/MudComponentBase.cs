﻿// Copyright (c) MudBlazor 2021
// MudBlazor licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Interfaces;

namespace MudBlazor
{
#nullable enable
    /// <summary>
    /// Represents a base class for designing MudBlazor components.
    /// </summary>
    public abstract class MudComponentBase : ComponentBaseWithState, IMudStateHasChanged
    {
        [Inject]
        private ILoggerFactory LoggerFactory { get; set; } = null!;
        private ILogger? _logger;
        protected ILogger Logger => _logger ??= LoggerFactory.CreateLogger(GetType());

        /// <summary>
        /// Gets or sets CSS classes applied to this component.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  You can use spaces to separate multiple classes.  Use the <see cref="Style"/> property to apply custom CSS styles.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.ComponentBase.Common)]
        public string? Class { get; set; }

        /// <summary>
        /// Gets or sets any CSS styles applied to this component. 
        /// </summary>
        /// <remarks>
        /// Defaults to <c>null</c>.  Use the <see cref="Class"/> property to apply CSS classes.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.ComponentBase.Common)]
        public string? Style { get; set; }

        /// <summary>
        /// Gets or sets an arbitrary object to link to this component.
        /// </summary>
        /// <remarks>
        /// This property is typically used to associate additional information with this component, such as a model containing data for this component.
        /// </remarks>
        [Parameter]
        [Category(CategoryTypes.ComponentBase.Common)]
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets any additional HTML attributes to apply to this component.
        /// </summary>
        /// <remarks>
        /// This property is typically used to provide additional HTML attributes during rendering such as ARIA accessibility tags or a custom ID.
        /// </remarks>
        [Parameter(CaptureUnmatchedValues = true)]
        [Category(CategoryTypes.ComponentBase.Common)]
        public Dictionary<string, object?> UserAttributes { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Gets or sets whether <see cref="JSRuntime" /> is available.
        /// </summary>
        protected bool IsJSRuntimeAvailable { get; set; }

        /// <summary>
        /// If the UserAttributes contain an ID make it accessible for WCAG labelling of input fields
        /// </summary>
        public string FieldId => UserAttributes.TryGetValue("id", out var id) && id != null
                    ? id.ToString() ?? $"mudinput-{Guid.NewGuid()}"
                    : $"mudinput-{Guid.NewGuid()}";

        /// <inheritdoc />
        protected override void OnAfterRender(bool firstRender)
        {
            IsJSRuntimeAvailable = true;
            base.OnAfterRender(firstRender);
        }

        /// <inheritdoc />
        void IMudStateHasChanged.StateHasChanged() => StateHasChanged();

        protected override void OnInitialized()
        {
            base.OnInitialized();
            if (MudGlobal.EnableIllegalRazorParameterDetection)
            {
                DetectIllegalRazorParametersV7();
            }
        }

        /// <summary>
        /// Razor silently ignores parameters which don't exist. Since v7.0.0 renamed so many parameters we want
        /// to help our users find old parameters they missed by throwing a runtime exception.
        ///
        /// TODO: Remove this later. At the moment, we don't know yet when will be the best time to remove it.
        /// Sometime when the v7 version has stabilized.
        /// </summary>
        [ExcludeFromCodeCoverage]
        protected void DetectIllegalRazorParametersV7()
        {
            foreach (var parameter in UserAttributes.Keys)
            {
                if (this is MudBadge)
                {
                    switch (parameter)
                    {
                        case "Bottom":
                        case "Left":
                        case "Start":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudProgressCircular || this is MudProgressLinear)
                {
                    switch (parameter)
                    {
                        case "Minimum":
                        case "Maximum":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudRadio<>))
                {
                    switch (parameter)
                    {
                        case "Option":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudFab)
                {
                    switch (parameter)
                    {
                        case "Icon":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudCheckBox<>) || GetType() == typeof(MudSwitch<>))
                {
                    switch (parameter)
                    {
                        case "Checked":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudPopover || GetType() == typeof(MudAutocomplete<>) || GetType() == typeof(MudSelect<>))
                {
                    switch (parameter)
                    {
                        case "Direction":
                        case "OffsetX":
                        case "OffsetY":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudToggleGroup<>))
                {
                    switch (parameter)
                    {
                        case "Outline":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudAvatar)
                {
                    switch (parameter)
                    {
                        case "Image":
                        case "Alt":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudSlider<>))
                {
                    switch (parameter)
                    {
                        case "Text":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudRadioGroup<>))
                {
                    switch (parameter)
                    {
                        case "SelectedOption":
                        case "SelectedOptionChanged":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudSwipeArea)
                {
                    switch (parameter)
                    {
                        case "OnSwipe":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudChip<>))
                {
                    switch (parameter)
                    {
                        case "Avatar":
                        case "AvatarClass":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudChipSet<>))
                {
                    switch (parameter)
                    {
                        case "Filter":
                        case "MultiSelection":
                        case "Mandatory":
                        case "SelectedChip":
                        case "SelectedChipChanged":
                        case "SelectedChips":
                        case "SelectedChipsChanged":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudList<>))
                {
                    switch (parameter)
                    {
                        case "SelectedItem":
                        case "SelectedItemChanged":
                        case "Clickable":
                        case "Avatar":
                        case "AvatarClass":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (GetType() == typeof(MudTreeView<>) || GetType() == typeof(MudTreeViewItem<>))
                {
                    switch (parameter)
                    {
                        case "CanActivate":
                        case "CanHover":
                        case "CanSelect":
                        case "ActivatedValue":
                        case "ActivatedValueChanged":
                        case "Multiselection":
                        case "Activated":
                        case "ExpandedIcon":
                        case "SelectedItem":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else if (this is MudMenu || this is MudMenuItem)
                {
                    switch (parameter)
                    {
                        case "Link":
                        case "Target":
                        case "HtmlTag":
                        case "ButtonType":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
                else
                {
                    switch (parameter)
                    {
                        case "UnCheckedColor":
                        case "Command":
                        case "CommandParameter":
                        case "IsEnabled":
                        case "ClassAction":
                        case "InputIcon":
                        case "InputVariant":
                        case "AllowKeyboardInput":
                        case "ClassActions":
                        case "DisableRipple":
                        case "DisableGutters":
                        case "DisablePadding":
                        case "DisableElevation":
                        case "DisableUnderLine":
                        case "DisableRowsPerPage":
                        case "Link":
                        case "Delayed":
                        case "AlertTextPosition":
                        case "ShowDelimiters":
                        case "DelimitersColor":
                        case "DrawerWidth":
                        case "DrawerHeightTop":
                        case "DrawerHeightBottom":
                        case "AppbarMinHeight":
                        case "ClassBackground":
                        case "Cancelled":
                        case "ClassContent":
                        case "IsExpanded":
                        case "IsExpandedChanged":
                        case "IsInitiallyExpanded":
                        case "InitiallyExpanded":
                        case "RightAlignSmall":
                        case "IsExpandable":
                            NotifyIllegalParameter(parameter);
                            break;
                    }
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private void NotifyIllegalParameter(string parameter)
        {
            throw new ArgumentException($"Illegal parameter '{parameter}'. This was removed in v7.0.0, see Migration Guide for more info https://github.com/MudBlazor/MudBlazor/issues/8447");
        }
    }
}
