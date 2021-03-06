﻿using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace MediaBrowser.Common.Implementations.Serialization
{
    /// <summary>
    /// Provides a wrapper around third party json serialization.
    /// </summary>
    public class JsonSerializer : IJsonSerializer
    {
        public JsonSerializer()
        {
            Configure();
        }

        /// <summary>
        /// Serializes to stream.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="stream">The stream.</param>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public void SerializeToStream(object obj, Stream stream)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            ServiceStack.Text.JsonSerializer.SerializeToStream(obj, obj.GetType(), stream);
        }

        /// <summary>
        /// Serializes to file.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <param name="file">The file.</param>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public void SerializeToFile(object obj, string file)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException("file");
            }

            using (Stream stream = File.Open(file, FileMode.Create))
            {
                SerializeToStream(obj, stream);
            }
        }

        /// <summary>
        /// Deserializes from file.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="file">The file.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.ArgumentNullException">type</exception>
        public object DeserializeFromFile(Type type, string file)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException("file");
            }

            using (Stream stream = File.OpenRead(file))
            {
                return ServiceStack.Text.JsonSerializer.DeserializeFromStream(type, stream);
            }
        }

        /// <summary>
        /// Deserializes from file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="file">The file.</param>
        /// <returns>``0.</returns>
        /// <exception cref="System.ArgumentNullException">file</exception>
        public T DeserializeFromFile<T>(string file)
            where T : class
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException("file");
            }

            using (Stream stream = File.OpenRead(file))
            {
                return ServiceStack.Text.JsonSerializer.DeserializeFromStream<T>(stream);
            }
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>``0.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public T DeserializeFromStream<T>(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            return ServiceStack.Text.JsonSerializer.DeserializeFromStream<T>(stream);
        }

        /// <summary>
        /// Deserializes from string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="text">The text.</param>
        /// <returns>``0.</returns>
        /// <exception cref="System.ArgumentNullException">text</exception>
        public T DeserializeFromString<T>(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentNullException("text");
            }

            return ServiceStack.Text.JsonSerializer.DeserializeFromString<T>(text);
        }

        /// <summary>
        /// Deserializes from stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="type">The type.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public object DeserializeFromStream(Stream stream, Type type)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            return ServiceStack.Text.JsonSerializer.DeserializeFromStream(type, stream);
        }

        /// <summary>
        /// Configures this instance.
        /// </summary>
        private void Configure()
        {
            ServiceStack.Text.JsConfig.DateHandler = ServiceStack.Text.JsonDateHandler.ISO8601;
            ServiceStack.Text.JsConfig.ExcludeTypeInfo = true;
            ServiceStack.Text.JsConfig.IncludeNullValues = false;
        }

        /// <summary>
        /// Deserializes from string.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="type">The type.</param>
        /// <returns>System.Object.</returns>
        /// <exception cref="System.ArgumentNullException">json</exception>
        public object DeserializeFromString(string json, Type type)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentNullException("json");
            }

            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            return ServiceStack.Text.JsonSerializer.DeserializeFromString(json, type);
        }

        /// <summary>
        /// Serializes to string.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public string SerializeToString(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            return ServiceStack.Text.JsonSerializer.SerializeToString(obj, obj.GetType());
        }

        /// <summary>
        /// Serializes to bytes.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>System.Byte[][].</returns>
        /// <exception cref="System.ArgumentNullException">obj</exception>
        public byte[] SerializeToBytes(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            using (var stream = new MemoryStream())
            {
                SerializeToStream(obj, stream);
                return stream.ToArray();
            }
        }
    }
}
