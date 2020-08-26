﻿using System.Data.HashFunction.xxHash;
using System.Drawing;
using System.Linq;

namespace JBToolkit.Imaging
{
    /// <summary>
    /// Image helper methods
    /// </summary>
    public static class ImageHelper
    {
        private static xxHashConfig XxHashConfig = new xxHashConfig() { HashSizeInBits = 32 };
        private static IxxHash XxHashFactory = xxHashFactory.Instance.Create(XxHashConfig);

        /// <summary>
        /// Get a 32bit xxHash of an image (useful for comparing)
        /// </summary>
        public static byte[] XxHash(this Image image)
        {
            var bytes = new byte[1];
            bytes = (byte[])new ImageConverter().ConvertTo(image, bytes.GetType());
            return XxHashFactory.ComputeHash(bytes).Hash;
        }

        /// <summary>
        /// Checks if 1 image is the same as another by comparing its SHA hash. Also performs
        /// a quick check on image dimentions to avoid performs cost of unnecessarily generating
        /// a hash
        /// </summary>
        /// <returns>True if the same, false otherwise</returns>
        public static bool IsSameImage(Image imageA, Image imageB)
        {
            if (imageA.Width != imageB.Width) return false;
            if (imageA.Height != imageB.Height) return false;

            var hashA = imageA.XxHash();
            var hashB = imageB.XxHash();

            return !hashA
                .Where((nextByte, index) => nextByte != hashB[index])
                .Any();
        }
    }
}
