﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MatBlazor.Components.MatTextFieldView;

namespace MatBlazor
{
    public abstract class BaseMatInputComponent<TValue> : BaseMatComponent, IBaseMatDomComponent, IMatInputViewModel<T>
    {
        private bool _previousParsingAttemptFailed;
        private ValidationMessageStore _parsingValidationMessages;
        private Type _nullableUnderlyingType;

        [CascadingParameter]
        EditContext CascadedEditContext { get; set; }

        /// <summary>
        /// Gets or sets the value of the input. This should be used with two-way binding.
        /// </summary>
        /// <example>
        /// @bind-Value="model.PropertyName"
        /// </example>
        [Parameter]
        public TValue Value { get; set; }

        /// <summary>
        /// Gets or sets a callback that updates the bound value.
        /// </summary>
        [Parameter]
        public EventCallback<TValue> ValueChanged { get; set; }

        /// <summary>
        /// Gets or sets an expression that identifies the bound value.
        /// </summary>
        [Parameter]
        public Expression<Func<TValue>> ValueExpression { get; set; }

        /// <summary>
        /// Gets the associated <see cref="Microsoft.AspNetCore.Components.Forms.EditContext"/>.
        /// </summary>
        protected EditContext EditContext { get; set; }

        /// <summary>
        /// Gets the <see cref="FieldIdentifier"/> for the bound value.
        /// </summary>
        protected FieldIdentifier FieldIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the current value of the input.
        /// </summary>
        protected TValue CurrentValue
        {
            get => Value;
            set
            {
                var hasChanged = !EqualityComparer<TValue>.Default.Equals(value, Value);
                if (hasChanged)
                {
                    Value = value;
                    ValueChanged.InvokeAsync(value);
                    EditContext?.NotifyFieldChanged(FieldIdentifier);
                }
            }
        }

        /// <summary>
        /// Gets or sets the current value of the input, represented as a string.
        /// </summary>
        public string CurrentValueAsString
        {
            get => FormatValueAsString(CurrentValue);
            set
            {
                _parsingValidationMessages?.Clear();

                bool parsingFailed;

                if (_nullableUnderlyingType != null && string.IsNullOrEmpty(value))
                {
                    // Assume if it's a nullable type, null/empty inputs should correspond to default(T)
                    // Then all subclasses get nullable support almost automatically (they just have to
                    // not reject Nullable<T> based on the type itself).
                    parsingFailed = false;
                    CurrentValue = default;
                }
                else if (TryParseValueFromString(value, out var parsedValue, out var validationErrorMessage))
                {
                    parsingFailed = false;
                    CurrentValue = parsedValue;
                }
                else
                {
                    parsingFailed = true;

                    if (_parsingValidationMessages == null && EditContext != null)
                    {
                        _parsingValidationMessages = new ValidationMessageStore(EditContext);
                    }

                    _parsingValidationMessages?.Add(FieldIdentifier, validationErrorMessage);

                    // Since we're not writing to CurrentValue, we'll need to notify about modification from here
                    EditContext?.NotifyFieldChanged(FieldIdentifier);
                }

                // We can skip the validation notification if we were previously valid and still are
                if (parsingFailed || _previousParsingAttemptFailed)
                {
                    EditContext?.NotifyValidationStateChanged();
                    _previousParsingAttemptFailed = parsingFailed;
                }
            }
        }

        /// <summary>
        /// Formats the value as a string. Derived classes can override this to determine the formating used for <see cref="CurrentValueAsString"/>.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>A string representation of the value.</returns>
        protected virtual string FormatValueAsString(TValue value)
            => value?.ToString();


        /// <summary>
        /// Gets a string that indicates the status of the field being edited. This will include
        /// some combination of "modified", "valid", or "invalid", depending on the status of the field.
        /// </summary>
        private string FieldClass
            => EditContext.FieldCssClass(FieldIdentifier);

        


        /// <inheritdoc />
        public override Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            if (EditContext == null)
            {
                // This is the first run
                // Could put this logic in OnInit, but its nice to avoid forcing people who override OnInit to call base.OnInit()

                if (CascadedEditContext != null)
                {
                    if (ValueExpression == null)
                    {
                        throw new InvalidOperationException($"{GetType()} requires a value for the 'ValueExpression' " +
                                                            $"parameter. Normally this is provided automatically when using 'bind-Value'.");
                    }

                    EditContext = CascadedEditContext;
                    FieldIdentifier = FieldIdentifier.Create(ValueExpression);
                    _nullableUnderlyingType = Nullable.GetUnderlyingType(typeof(TValue));
                }
            }
            else if (CascadedEditContext != EditContext)
            {
                // Not the first run

                // We don't support changing EditContext because it's messy to be clearing up state and event
                // handlers for the previous one, and there's no strong use case. If a strong use case
                // emerges, we can consider changing this.
                throw new InvalidOperationException($"{GetType()} does not support changing the " +
                                                    $"{nameof(EditContext)} dynamically.");
            }

            // For derived components, retain the usual lifecycle with OnInit/OnParametersSet/etc.
            return base.SetParametersAsync(ParameterView.Empty);
        }


        /// <summary>
        /// Parses a string to create an instance of <typeparamref name="TValue"/>. Derived classes can override this to change how
        /// <see cref="CurrentValueAsString"/> interprets incoming values.
        /// </summary>
        /// <param name="value">The string value to be parsed.</param>
        /// <param name="result">An instance of <typeparamref name="TValue"/>.</param>
        /// <param name="validationErrorMessage">If the value could not be parsed, provides a validation error message.</param>
        /// <returns>True if the value could be parsed; otherwise false.</returns>
        protected virtual bool TryParseValueFromString(string value, out TValue result,
            out string validationErrorMessage)
        {
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(TValue));
                result = (TValue) converter.ConvertFromString(value);
                validationErrorMessage = null;
                return true;
            }
            catch (Exception e)
            {
                result = default(TValue);
                validationErrorMessage = e.Message;
                return false;
            }
        }

        [Parameter]
        public string Id { get; set; } = IdGeneratorHelper.Generate("");

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object> Attributes { get; set; }

        public abstract ElementReference Ref { get; }

        [Parameter]
        public string Class { get; set; }

        [Parameter]
        public string Style { get; set; }
    }
}