﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiffBlit.Core.Extensions;
using DiffBlit.Core.Utilities;
using Newtonsoft.Json;

namespace DiffBlit.Core.Config
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Content : IEquatable<Content>
    {
        /// <summary>
        /// TODO: description
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// TODO: description
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public bool Compressed { get; set; } = true;

        /// <summary>
        /// TODO: description
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<FileInformation> Parts { get; } = new List<FileInformation>();

        /// <summary>
        /// TODO: description
        /// </summary>
        [JsonConstructor]
        private Content()
        {
            
        }

        /// <summary>
        /// TODO: description
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="settings"></param>
        public Content(Path inputFile, Path outputDirectory, PackageSettings settings = null)
        {
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("Unable to create content.", inputFile);

            PackageSettings pkgSettings = settings ?? new PackageSettings();
            Compressed = pkgSettings.CompressionEnabled;
            string contentDirectory = Path.Combine(outputDirectory, Id.ToString());
            Directory.CreateDirectory(contentDirectory);

            try
            {
                if (pkgSettings.CompressionEnabled)
                {
                    string origPath = inputFile;
                    inputFile = Utility.GetTempFilePath();
                    Utility.Compress(origPath, inputFile);
                }

                // break the file up into parts of specified chunk size
                using (FileStream fs = File.OpenRead(inputFile))
                {
                    long bytesCopied = 0;
                    int fileIndex = 0;

                    do
                    {
                        string fileName = $"{fileIndex}{pkgSettings.PartExtension}";
                        string path = Path.Combine(contentDirectory, fileName);
                        long bytesToCopy = Math.Min(fs.Length - bytesCopied, pkgSettings.PartSize);

                        using (var file = File.OpenWrite(path))
                        {
                            fs.CopyToCount(file, bytesToCopy);
                        }

                        Parts.Add(new FileInformation(fileName, Utility.ComputeHash(path)));
                        bytesCopied += bytesToCopy;
                        fileIndex++;

                    } while (bytesCopied < fs.Length); 
                }
            }
            finally
            {
                if (pkgSettings.CompressionEnabled)
                {
                    File.Delete(inputFile);
                }
            }
        }

        /// <summary>
        /// Combines and saves content files into a single file.
        /// </summary>
        /// <param name="sourceDirectory">The directory which contains the content file(s).</param>
        /// <param name="outputFilePath">The path of the file to be created.</param>
        public void Save(Path sourceDirectory, Path outputFilePath)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException("Invalid source directory.");

            if (string.IsNullOrWhiteSpace(outputFilePath))
                throw new ArgumentException(nameof(outputFilePath));                   

            if (Parts.Count == 0)
                throw new InvalidOperationException();

            // make sure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));

            if (Compressed)
            {
                var compressedFile = Utility.GetTempFilePath();

                try
                {
                    // must assemble compressed file first, can't decompress against individual parts
                    Utility.JoinFiles(sourceDirectory, compressedFile);
                    Utility.Decompress(compressedFile, outputFilePath);
                }
                finally
                {
                    File.Delete(compressedFile);
                }
            }
            else
            {
                Utility.JoinFiles(sourceDirectory, outputFilePath);
            }
        }

        /// <inheritdoc/>
        public bool Equals(Content other)
        {
            if (Parts.Count != other?.Parts.Count)
                return false;

            return !Parts.Where((t, i) => !t.Equals(other.Parts[i])).Any();
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Content) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Parts.Aggregate(0, (current, item) => current ^ item.GetHashCode());
        }
    }
}
