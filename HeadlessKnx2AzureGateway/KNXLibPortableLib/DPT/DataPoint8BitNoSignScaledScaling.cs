namespace KNXLibPortableLib.DPT
{
    using System.Globalization;

    public sealed class DataPoint8BitNoSignScaledScaling : DataPoint
    {
        public override string[] Ids
        {
            get { return new[] { "5.001" }; }
        }

        public override object FromDataPoint(string data, bool isDebug = false)
        {
            var dataConverted = new byte[data.Length];
            for (var i = 0; i < data.Length; i++)
                dataConverted[i] = (byte)data[i];

            return this.FromDataPoint(dataConverted, isDebug);
        }

        public override object FromDataPoint(byte[] data, bool isDebug = false)
        {
            if (data == null || data.Length != 1)
                return 0;

            var value = (int)data[0];

            decimal result = value * 100;
            result = result / 255;

            return result;
        }

        public override byte[] ToDataPoint(string value, bool isDebug = false)
        {
            return this.ToDataPoint(float.Parse(value, CultureInfo.InvariantCulture), isDebug);
        }

        public override byte[] ToDataPoint(object val, bool isDebug = false)
        {
            var dataPoint = new byte[1];
            dataPoint[0] = 0x00;

            decimal input = 0;
            if (val is int)
                input = (decimal)((int)val);
            else if (val is float)
                input = (decimal)((float)val);
            else if (val is long)
                input = (decimal)((long)val);
            else if (val is double)
                input = (decimal)((double)val);
            else if (val is decimal)
                input = (decimal)val;
            else
            {
                if (isDebug)
                {
                    System.Diagnostics.Debug.WriteLine("5.001", "input value received is not a valid type");
                }
                return dataPoint;
            }

            if (input < 0 || input > 100)
            {
                if (isDebug)
                {
                    System.Diagnostics.Debug.WriteLine("5.001", "input value received is not in a valid range");
                }
                return dataPoint;
            }

            input = input * 255;
            input = input / 100;

            dataPoint[0] = (byte)(input);

            return dataPoint;
        }
    }
}