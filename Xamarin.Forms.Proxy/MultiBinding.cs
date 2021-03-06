﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Xamarin.Forms.Proxy
{
[ContentProperty(nameof(Bindings))]
public class MultiBinding : BindingBase
{
    private readonly BindingExpression _bindingExpression;
    private readonly InternalValue _internalValue = new InternalValue();
    private readonly IList<BindableProperty> _properties = new List<BindableProperty>();

    private bool _isApplying;
    private IMultiValueConverter _converter;
    private object _converterParameter;

    public IList<BindingBase> Bindings { get; } = new List<BindingBase>();

    public IMultiValueConverter Converter
    {
        get { return _converter; }
        set
        {
            ThrowIfApplied();
            _converter = value;
        }
    }

    public object ConverterParameter
    {
        get { return _converterParameter; }
        set
        {
            ThrowIfApplied();
            _converterParameter = value;
        }
    }

    public MultiBinding()
    {
        Mode = BindingMode.OneWay;
        _bindingExpression = new BindingExpression(this, nameof(InternalValue.Value));
    }

    internal override void Apply(object newContext, BindableObject bindObj, BindableProperty targetProperty)
    {
        if (Mode != BindingMode.OneWay)
            throw new InvalidOperationException($"{nameof(MultiBinding)} only supports {nameof(Mode)}.{nameof(BindingMode.OneWay)}");

        object source = Context ?? newContext;
        base.Apply(source, bindObj, targetProperty);

        _isApplying = true;
        foreach (BindingBase binding in Bindings)
        {
            var property = BindableProperty.Create($"{nameof(MultiBinding)}Property-{Guid.NewGuid().ToString("N")}", typeof(object),
                typeof(MultiBinding), default(object), propertyChanged: (bindableObj, o, n) => SetValue(bindableObj));
            _properties.Add(property);
            binding.Apply(source, bindObj, property);
        }
        _isApplying = false;
        SetValue(bindObj);

        _bindingExpression.Apply(_internalValue, bindObj, targetProperty);
    }

    internal override void Apply(bool fromTarget)
    {
        base.Apply(fromTarget);
        foreach (BindingBase binding in Bindings)
        {
            binding.Apply(fromTarget);
        }
        _bindingExpression.Apply(fromTarget);
    }

    internal override void Unapply()
    {
        base.Unapply();
        foreach (BindingBase binding in Bindings)
        {
            binding.Unapply();
        }
        _properties.Clear();
        _bindingExpression?.Unapply();
    }

    internal override object GetSourceValue(object value, Type targetPropertyType)
    {
        if (Converter != null)
            value = Converter.Convert(value as object[], targetPropertyType, ConverterParameter, CultureInfo.CurrentUICulture);
        if (StringFormat != null && value != null)
        {
            var array = value as object[];
            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (array != null)
            {
                value = string.Format(StringFormat, array);
            }
            else
            {
                value = string.Format(StringFormat, value);
            }
        }
        return value;
    }

    internal override object GetTargetValue(object value, Type sourcePropertyType)
    {
        throw new InvalidOperationException($"{nameof(MultiBinding)} only supports {nameof(Mode)}.{nameof(BindingMode.OneWay)}");
    }

    private void SetValue(BindableObject source)
    {
        if (source == null || _isApplying) return;
        _internalValue.Value = _properties.Select(source.GetValue).ToArray();
    }

    internal override BindingBase Clone()
    {
        var rv = new MultiBinding
        {
            Converter = Converter,
            ConverterParameter = ConverterParameter,
            StringFormat = StringFormat
        };
        rv._internalValue.Value = _internalValue.Value;

        foreach (var binding in Bindings.Select(x => x.Clone()))
        {
            rv.Bindings.Add(binding);
        }
        return rv;
    }

    private sealed class InternalValue : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private object _value;
        public object Value
        {
            get { return _value; }
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
}
