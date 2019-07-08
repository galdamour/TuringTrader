﻿//==============================================================================
// Project:     TuringTrader, simulator core
// Name:        DataSourceYahoo
// Description: Data source for Yahoo EOD Data.
// History:     2019vi02, FUB, created
//------------------------------------------------------------------------------
// Copyright:   (c) 2011-2019, Bertram Solutions LLC
//              http://www.bertram.solutions
// License:     This code is licensed under the term of the
//              GNU Affero General Public License as published by 
//              the Free Software Foundation, either version 3 of 
//              the License, or (at your option) any later version.
//              see: https://www.gnu.org/licenses/agpl-3.0.en.html
//==============================================================================

#if true

#region libraries
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
#endregion

namespace TuringTrader.Simulator
{
    public partial class DataSourceCollection
    {
        private class DataSourceYahoo : DataSource
        {
            #region internal helpers
            private static readonly DateTime _epochOrigin 
                = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            private static DateTime FromUnixTime(long unixTime)
            {
                return _epochOrigin.AddSeconds(unixTime);
            }

            private static long ToUnixTime(DateTime date)
            {
                return Convert.ToInt64((date - _epochOrigin).TotalSeconds);
            }

            private string ConvertSymbol(string ticker)
            {
                return ticker.Replace('.', '-');
            }

            private JObject GetPrices(DateTime startTime, DateTime endTime)
            {
                string cachePath = Path.Combine(GlobalSettings.HomePath, "Cache", Info[DataSourceParam.nickName2]);
                string timeStamps = Path.Combine(cachePath, "yahoo_timestamps");
                string priceCache = Path.Combine(cachePath, "yahoo_prices");

                string rawPricesFromDisk = null;
                string rawPrices = null;
                JObject jsonPrices = null;

                bool validPrices()
                {
                    if (rawPrices == null)
                        return false;

                    if (rawPrices.Length < 25)
                        return false;

                    if (!jsonPrices.HasValues)
                        return false;

                    return true;
                }

                //--- 1) try to read raw json from disk
                if (File.Exists(timeStamps) && File.Exists(priceCache))
                {
                    using (BinaryReader pc = new BinaryReader(File.Open(priceCache, FileMode.Open)))
                        rawPricesFromDisk = pc.ReadString();

                    using (BinaryReader ts = new BinaryReader(File.Open(timeStamps, FileMode.Open)))
                    {
                        DateTime cacheStartTime = new DateTime(ts.ReadInt64());
                        DateTime cacheEndTime = new DateTime(ts.ReadInt64());

                        if (cacheStartTime.Date <= startTime.Date && cacheEndTime.Date >= endTime.Date)
                            rawPrices = rawPricesFromDisk;

                        jsonPrices = JObject.Parse(rawPrices);
                    }
                }

                //--- 2) if failed, try to retrieve from web
                if (!validPrices())
                {
#if true
                    // always request whole range here, to make
                    // offline behavior as pleasant as possible
                    DateTime DATA_START = DateTime.Parse("01/01/1970");

                    //startTime = ((DateTime)FirstTime) < DATA_START
                    //    ? DATA_START
                    //    : (DateTime)FirstTime;
                    startTime = DATA_START;
                    endTime = DateTime.Now.Date + TimeSpan.FromDays(1);
#else
                    startTime = startTime.Date;
                    endTime = endTime.Date + TimeSpan.FromDays(5);
#endif

                    string url = string.Format(
                        @"http://l1-query.finance.yahoo.com/v8/finance/chart/"
                        + "{0}"
                        + "?interval=1d"
                        + "&period1={1}"
                        + "&period2={2}",
                        ConvertSymbol(Info[DataSourceParam.symbolYahoo]),
                        ToUnixTime(startTime),
                        ToUnixTime(endTime));

                    using (var client = new WebClient())
                        rawPrices = client.DownloadString(url);

                    jsonPrices = JObject.Parse(rawPrices);
                }

                //--- 3) if failed, try to fall back to data from disk
                // we might have discarded the data from disk before,
                // because the time frame wasn't what we were looking for. 
                // however, in case we can't load from web, e.g. because 
                // we don't have internet connectivity, it's still better 
                // to go with what we have cached before
                if (!validPrices() && rawPricesFromDisk != null)
                {
                    rawPrices = rawPricesFromDisk;
                    jsonPrices = JObject.Parse(rawPrices);
                }

                //--- 4) if failed, return
                if (!validPrices())
                    return null;

                //--- 5) write to disk
                if (rawPricesFromDisk == null)
                {
                    Directory.CreateDirectory(cachePath);
                    using (BinaryWriter pc = new BinaryWriter(File.Open(priceCache, FileMode.Create)))
                        pc.Write(rawPrices);

                    using (BinaryWriter ts = new BinaryWriter(File.Open(timeStamps, FileMode.Create)))
                    {
                        ts.Write(startTime.Ticks);
                        ts.Write(endTime.Ticks);
                    }
                }

                return jsonPrices;
            }
            private string GetMeta()
            {
                string cachePath = Path.Combine(GlobalSettings.HomePath, "Cache", Info[DataSourceParam.nickName2]);
                string metaCache = Path.Combine(cachePath, "yahoo_meta");

                bool writeToDisk = false;
                string rawMeta = null;

                bool validMeta()
                {
                    if (rawMeta == null)
                        return false;

                    if (rawMeta.Length < 10)
                        return false;

                    return true;
                }

                //--- 1) try to read meta from disk
                if (File.Exists(metaCache))
                {
                    using (StreamReader mc = new StreamReader(File.Open(metaCache, FileMode.Open)))
                        rawMeta = mc.ReadToEnd();
                }

                //--- 2) if failed, try to retrieve from web
                if (!validMeta())
                {

                    string url = string.Format(
                        @"http://finance.yahoo.com/quote/"
                        + "{0}",
                        ConvertSymbol(Info[DataSourceParam.symbolYahoo]));

                    using (var client = new WebClient())
                        rawMeta = client.DownloadString(url);

                    writeToDisk = true;
                }

                //--- 3) if failed, return
                if (!validMeta())
                    return null;

                //--- 4) write to disk
                if (writeToDisk)
                {
                    Directory.CreateDirectory(cachePath);
                    using (StreamWriter mc = new StreamWriter(File.Open(metaCache, FileMode.Create)))
                        mc.Write(rawMeta);
                }

                return rawMeta;
            }
            #endregion

            //---------- API
            #region public DataSourceYahoo(Dictionary<DataSourceValue, string> info)
            /// <summary>
            /// Create and initialize new data source for Yahoo Data.
            /// </summary>
            /// <param name="info">info dictionary</param>
            public DataSourceYahoo(Dictionary<DataSourceParam, string> info) : base(info)
            {
                // Yahoo does not provide meta data
                // we extract them from the instrument's web page

                string meta = GetMeta();

                {
                    string tmp1 = meta.Substring(meta.IndexOf("<h1"));
                    string tmp2 = tmp1.Substring(0, tmp1.IndexOf("h1>"));

                    string tmp3 = tmp2.Substring(tmp2.IndexOf(">") + 1);
                    string tmp4 = tmp3.Substring(0, tmp3.IndexOf("<"));

                    tmp4 = tmp4.Replace("&amp;", "&");

                    Info[DataSourceParam.name] = tmp4;
                }
            }
            #endregion
            #region override public void LoadData(DateTime startTime, DateTime endTime)
            /// <summary>
            /// Load data into memory.
            /// </summary>
            /// <param name="startTime">start of load range</param>
            /// <param name="endTime">end of load range</param>
            override public void LoadData(DateTime startTime, DateTime endTime)
            {
                try
                {
                    //if (startTime < (DateTime)FirstTime)
                    //    startTime = (DateTime)FirstTime;

                    //if (endTime > (DateTime)LastTime)
                    //    endTime = (DateTime)LastTime;

                    var cacheKey = new CacheId(null, "", 0,
                        Info[DataSourceParam.nickName].GetHashCode(),
                        startTime.GetHashCode(),
                        endTime.GetHashCode());

                    List<Bar> retrievalFunction()
                    {
                        DateTime t1 = DateTime.Now;
                        Output.Write(string.Format("DataSourceYahoo: loading data for {0}...", Info[DataSourceParam.nickName]));

                        JObject jsonData = GetPrices(startTime, endTime);

                        /*
                        Yahoo JSON format, as of 07/02/2019

                        [JSON]
                            chart
                                result
                                    [0]
                                        meta
                                            currency
                                            symbol
                                            ...
                                        timestamp
                                            [0]: 511108200
                                            [1]: 511194600
                                            ...
                                        indicators
                                            quote
                                                [0]
                                                    low
                                                        [0]: 0.08854
                                                        [1]: 0.09722
                                                        ...
                                                    close
                                                    volume
                                                    open
                                                    high
                                            adjclose
                                                [0]
                                                    adjclose
                                                        [0]: 0.06999
                                                        [1]: 0.07249
                        */

                        List<Bar> bars = new List<Bar>();

                        var timestamps = (JArray)jsonData["chart"]["result"][0]["timestamp"];
                        var opens = (JArray)jsonData["chart"]["result"][0]["indicators"]["quote"][0]["open"];
                        var highs = (JArray)jsonData["chart"]["result"][0]["indicators"]["quote"][0]["high"];
                        var lows = (JArray)jsonData["chart"]["result"][0]["indicators"]["quote"][0]["low"];
                        var closes = (JArray)jsonData["chart"]["result"][0]["indicators"]["quote"][0]["close"];
                        var volumes = (JArray)jsonData["chart"]["result"][0]["indicators"]["quote"][0]["volume"];
                        var adjcloses = (JArray)jsonData["chart"]["result"][0]["indicators"]["adjclose"][0]["adjclose"];

                        var eT = timestamps.GetEnumerator();
                        var eO = opens.GetEnumerator();
                        var eH = highs.GetEnumerator();
                        var eL = lows.GetEnumerator();
                        var eC = closes.GetEnumerator();
                        var eV = volumes.GetEnumerator();
                        var eAC = adjcloses.GetEnumerator();

                        while (eT.MoveNext() && eO.MoveNext() && eH.MoveNext()
                            && eL.MoveNext() && eC.MoveNext() && eV.MoveNext() && eAC.MoveNext())
                        {
                            DateTime t = FromUnixTime((long)eT.Current).Date
                                + DateTime.Parse("16:00").TimeOfDay;

                            Bar bar = null;
                            try
                            {
                                // Yahoo taints the results by filling in null values
                                // we try to handle this gracefully in the catch block

                                double o = (double)eO.Current;
                                double h = (double)eH.Current;
                                double l = (double)eL.Current;
                                double c = (double)eC.Current;
                                long v = (long)eV.Current;
                                double ac = (double)eAC.Current;

                                // adjust prices according to the adjusted close.
                                // note the volume is adjusted the opposite way.
                                double ao = o * ac / c;
                                double ah = h * ac / c;
                                double al = l * ac / c;
                                long av = (long)(v * c / ac);

                                bar = Bar.NewOHLC(
                                        Info[DataSourceParam.ticker],
                                        t,
                                        ao, ah, al, ac,
                                        av);
                            }
                            catch
                            {
                                if (bars.Count < 1)
                                    continue;

                                Bar prevBar = bars.Last();

                                bar = Bar.NewOHLC(
                                        Info[DataSourceParam.ticker],
                                        t,
                                        prevBar.Open, prevBar.High, prevBar.Low, prevBar.Close,
                                        prevBar.Volume);
                            }

                            if (t >= startTime && t <= endTime)
                                bars.Add(bar);
                        }

                        DateTime t2 = DateTime.Now;
                        Output.WriteLine(string.Format(" finished after {0:F1} seconds", (t2 - t1).TotalSeconds));

                        return bars;
                    };

                    Data = Cache<List<Bar>>.GetData(cacheKey, retrievalFunction);
                }

                catch (Exception e)
                {
                    throw new Exception(
                        string.Format("DataSourceYahoo: failed to load quotes for {0}, {1}",
                            Info[DataSourceParam.nickName], e.Message));
                }

                if ((Data as List<Bar>).Count == 0)
                    throw new Exception(string.Format("DataSourceYahoo: no data for {0}", Info[DataSourceParam.nickName]));

            }
            #endregion
        }
    }
}
#endif

//==============================================================================
// end of file