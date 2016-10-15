namespace KNXLibPortableLib.DPT
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public sealed class DataPointTranslator
    {
        public static readonly DataPointTranslator Instance = new DataPointTranslator();
        private readonly IDictionary<string, DataPoint> _dataPoints = new Dictionary<string, DataPoint>();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DataPointTranslator()
        {
        }

        private DataPointTranslator()
        {
            Type type = typeof(DataPoint);
            IEnumerable<Type> types = type.GetTypeInfo().Assembly.GetTypes().Where(aR => type.IsAssignableFrom(aR) && aR != type);

            foreach (Type t in types)
            {
                DataPoint dp = (DataPoint)Activator.CreateInstance(t);

                foreach (string id in dp.Ids)
                {
                    this._dataPoints.Add(id, dp);
                }
            }
        }

        public object FromDataPoint(string type, string data)
        {
            try
            {
                DataPoint dpt;
                if (this._dataPoints.TryGetValue(type, out dpt))
                    return dpt.FromDataPoint(data);
            }
            catch
            {
            }

            return null;
        }

        public object FromDataPoint(string type, byte[] data)
        {
            try
            {
                DataPoint dpt;
                if (this._dataPoints.TryGetValue(type, out dpt))
                    return dpt.FromDataPoint(data);
            }
            catch
            {
            }

            return null;
        }

        public byte[] ToDataPoint(string type, string value)
        {
            try
            {
                DataPoint dpt;
                if (this._dataPoints.TryGetValue(type, out dpt))
                    return dpt.ToDataPoint(value);
            }
            catch
            {
            }

            return null;
        }

        public byte[] ToDataPoint(string type, object value)
        {
            try
            {
                DataPoint dpt;
                if (this._dataPoints.TryGetValue(type, out dpt))
                    return dpt.ToDataPoint(value);
            }
            catch
            {
            }

            return null;
        }
    }
}
