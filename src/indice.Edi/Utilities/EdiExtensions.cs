﻿using indice.Edi.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace indice.Edi.Utilities
{
    public static class EdiExtensions
    {
        public static bool IsStartToken(this EdiToken token) {
            switch (token) {
                case EdiToken.SegmentStart:
                case EdiToken.ElementStart:
                case EdiToken.ComponentStart:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsPrimitiveToken(this EdiToken token) {
            switch (token) {
                case EdiToken.Integer:
                case EdiToken.Float:
                case EdiToken.String:
                case EdiToken.Boolean:
                case EdiToken.Null:
                case EdiToken.Date:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attributes">The list of available attributes</param>
        /// <param name="container">This is the type of container we are searchig attributes for.</param>
        /// <returns>The attributes matching the <see cref="EdiStructureType"/></returns>
        public static IEnumerable<EdiAttribute> OfType(this IEnumerable<EdiAttribute> attributes, EdiStructureType container) {
            var typesToSearch = new Type[0];
            switch (container) {
                case EdiStructureType.None:
                case EdiStructureType.Interchange:
                    break;
                case EdiStructureType.Group:
                    typesToSearch = new [] { typeof(EdiGroupAttribute) };
                    break;
                case EdiStructureType.Message:
                    typesToSearch = new [] { typeof(EdiMessageAttribute) };
                    break;
                case EdiStructureType.SegmentGroup:
                    typesToSearch = new [] { typeof(EdiSegmentGroupAttribute) };
                    break;
                case EdiStructureType.Segment:
                    typesToSearch = new [] { typeof(EdiSegmentAttribute), typeof(EdiSegmentGroupAttribute) };
                    break;
                case EdiStructureType.Element:
                    typesToSearch = new [] { typeof(EdiElementAttribute) };
                    break;
                default:
                    break;
            }

            return null == typesToSearch ? Enumerable.Empty<EdiAttribute>() : attributes.Where(a => typesToSearch.Contains(a.GetType()));
        }

        /// <summary>
        /// Figures out the container (<see cref="EdiStructureType"/>) based upon the current pool of <seealso cref="EdiStructureAttribute"/>.
        /// </summary>
        /// <param name="attributes"></param>
        /// <returns></returns>
        public static EdiStructureType InferStructure(this IEnumerable<EdiAttribute> attributes) {
            var structureType = EdiStructureType.None;
            foreach (var attribute in attributes) {
                if (!(attribute is EdiStructureAttribute)) {
                    continue;
                }
                structureType = attribute is EdiSegmentGroupAttribute ? EdiStructureType.SegmentGroup :
                                attribute is EdiSegmentAttribute ? EdiStructureType.Segment :
                                attribute is EdiElementAttribute ? EdiStructureType.Element :
                                attribute is EdiMessageAttribute ? EdiStructureType.Message :
                                attribute is EdiGroupAttribute ? EdiStructureType.Group : EdiStructureType.None;
                if (structureType != EdiStructureType.None) {
                    return structureType;
                }
            }
            return structureType;
        }

        public static bool TryParse(this string value, Picture? picture, char? decimalMark, out decimal number) {
            number = 0.0M;
            try {
                var result = Parse(value, picture, decimalMark);
                if (result.HasValue)
                    number = result.Value;
                return true;
            } catch {
                return false;
            }
        }
        public static DateTime ParseEdiDate(this string value, string format, CultureInfo culture = null) {
            var date = ParseEdiDateInternal(value, format, culture);
            return date.Value;
        }

        public static bool TryParseEdiDate(this string value, string format, CultureInfo culture, out DateTime date) {
            date = default(DateTime);
            var dateNullable = ParseEdiDateInternal(value, format, culture);
            if (dateNullable.HasValue)
                date = dateNullable.Value;
            return dateNullable.HasValue;
        }

        private static DateTime? ParseEdiDateInternal(string value, string format, CultureInfo culture = null) {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentOutOfRangeException(nameof(format));

            var wrapped = new StringBuilder(value);
            if (format.Contains("HH")) {
                var startIndex = format.IndexOf('H');
                if (wrapped[startIndex] == '2' && wrapped[startIndex + 1] == '4')
                    wrapped[startIndex] = wrapped[startIndex + 1] = '0';
            }
            DateTime dt;
            if (DateTime.TryParseExact(wrapped.ToString(), format, culture ?? CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt)) {
                return wrapped.ToString() != value
                ? dt.AddDays(1)
                : dt;
            } 
            return null;
        }

        public static decimal? Parse(this string value, Picture? picture, char? decimalMark) {
            if (value != null)
                value = value.TrimStart('Z'); // Z suppresses leading zeros
            if (string.IsNullOrEmpty(value))
                return null;
            decimal d;
            var provider = NumberFormatInfo.InvariantInfo;
            if (decimalMark.HasValue) {
                if (provider.NumberDecimalSeparator != decimalMark.ToString()) {
                    provider = provider.Clone() as NumberFormatInfo;
                    provider.NumberDecimalSeparator = decimalMark.Value.ToString();
                }
                if (decimal.TryParse(value, NumberStyles.Number, provider, out d)) {
                    return d;
                }
            }
            else if (picture.HasValue && picture.Value.Kind == PictureKind.Numeric && decimal.TryParse(value, NumberStyles.Integer, provider, out d)) {
                d = d * (decimal)Math.Pow(0.1, picture.Value.Precision);
                return d;
            }
            throw new EdiException("Could not convert string to decimal: {0}.".FormatWith(CultureInfo.InvariantCulture, value));
        }

        public static string ToEdiString(this float value, Picture? picture, char? decimalMark) =>
            ToEdiString((decimal?)value, picture, decimalMark);

        public static string ToEdiString(this double value, Picture? picture, char? decimalMark) =>
            ToEdiString((decimal?)value, picture, decimalMark);
        public static string ToEdiString(this decimal value, Picture? picture, char? decimalMark) =>
            ToEdiString((decimal?)value, picture, decimalMark);

        public static string ToEdiString(this float? value, Picture? picture, char? decimalMark) =>
            ToEdiString((decimal?)value, picture, decimalMark);

        public static string ToEdiString(this double? value, Picture? picture, char? decimalMark) =>
            ToEdiString((decimal?)value, picture, decimalMark);

        public static string ToEdiString(this decimal? value, Picture? picture, char? decimalMark) {
            //if (string.IsNullOrEmpty(value))
            //    return null;
            //decimal d;
            //var provider = NumberFormatInfo.InvariantInfo;
            //if (decimalMark.HasValue) {
            //    if (provider.NumberDecimalSeparator != decimalMark.ToString()) {
            //        provider = provider.Clone() as NumberFormatInfo;
            //        provider.NumberDecimalSeparator = decimalMark.Value.ToString();
            //    }
            //    if (decimal.TryParse(value, NumberStyles.Number, provider, out d)) {
            //        return d;
            //    }
            //} else if (picture.HasValue && picture.Value.Kind == PictureKind.Numeric && decimal.TryParse(value, NumberStyles.Integer, provider, out d)) {
            //    d = d * (decimal)Math.Pow(0.1, picture.Value.Precision);
            //    return d;
            //}
            throw new EdiException("Could not convert string to decimal: {0}.".FormatWith(CultureInfo.InvariantCulture, value));
        }
    }
}
