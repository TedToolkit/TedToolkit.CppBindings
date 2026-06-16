// -----------------------------------------------------------------------
// <copyright file="RecordInfo.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Globalization;

namespace TedToolkit.Occt.Generator.Models;

/// <summary>
/// Stores native layout information for a single record.
/// </summary>
internal sealed class RecordInfo
{
    private readonly Dictionary<string, long> _offsets = new(StringComparer.Ordinal);

    private RecordInfo(long size)
    {
        Size = size;
    }

    /// <summary>
    /// Gets the record size in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Parses grouped native probe output into record layout info entries.
    /// </summary>
    /// <param name="output">The native probe output.</param>
    /// <returns>The parsed record layout info map keyed by C++ record name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The probe output is malformed.</exception>
    public static Dictionary<string, RecordInfo> ParseMany(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var infos = new Dictionary<string, RecordInfo>(StringComparer.Ordinal);
        RecordInfo? currentRecord = null;

        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts is ["S", var recordName, var sizeText,])
            {
                currentRecord = new(long.Parse(sizeText, CultureInfo.InvariantCulture));
                infos[recordName] = currentRecord;
                continue;
            }

            if (parts is ["F", var fieldName, var offsetText,])
            {
                if (currentRecord is null)
                {
                    throw new InvalidOperationException(
                        "Encountered a field offset before a record size in native layout probe output.");
                }

                currentRecord._offsets[fieldName] = long.Parse(offsetText, CultureInfo.InvariantCulture);
                continue;
            }

            throw new InvalidOperationException($"Unexpected native layout probe output line: {line}");
        }

        return infos;
    }

    /// <summary>
    /// Gets the field offset in bytes.
    /// </summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The field offset in bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fieldName"/> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The field offset was not captured.</exception>
    public long GetOffset(string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldName);

        if (_offsets.TryGetValue(fieldName, out var offset))
        {
            return offset;
        }

        throw new InvalidOperationException($"Native layout probe did not produce offset for field ({fieldName}).");
    }
}