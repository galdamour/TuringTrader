﻿//==============================================================================
// Project:     Trading Simulator
// Name:        DataUpdaterIQFeed
// Description: IQFeed/ DTN data updater
// History:     2018x02, FUB, created
//------------------------------------------------------------------------------
// Copyright:   (c) 2017-2018, Bertram Solutions LLC
//              http://www.bertram.solutions
// License:     this code is licensed under GPL-3.0-or-later
//==============================================================================

#region libraries
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IQFeed.CSharpApiClient;
using IQFeed.CSharpApiClient.Lookup;
using IQFeed.CSharpApiClient.Lookup.Historical.Messages;
#endregion

namespace FUB_TradingSim
{
    class DataUpdaterIQFeed : DataUpdater
    {
        #region public DataUpdaterIQFeed(Dictionary<DataSourceValue, string> info) : base(info)
        public DataUpdaterIQFeed(Dictionary<DataSourceValue, string> info) : base(info)
        {
        }
        #endregion

        #region override IEnumerable<Bar> void UpdateData(DateTime startTime, DateTime endTime)
        override public IEnumerable<Bar> UpdateData(DateTime startTime, DateTime endTime)
        {
            IQFeedLauncher.Start("473776", "80424284", "ONDEMAND_SERVER", "1.0");
            var lookupClient = LookupClientFactory.CreateNew();
            lookupClient.Connect();

            string symbol = Info[DataSourceValue.ticker];

            IEnumerable<IDailyWeeklyMonthlyMessage> dailyMessages =
                lookupClient.Historical.ReqHistoryDailyTimeframeAsync(
                    symbol, startTime, endTime).Result;

            List<Bar> newBars = new List<Bar>();
            foreach (IDailyWeeklyMonthlyMessage msg in dailyMessages)
            {
                DateTime barTime = msg.Timestamp.Date + DateTime.Parse("16:00").TimeOfDay;

                Bar newBar = new Bar(
                    Info[DataSourceValue.ticker], barTime,
                    msg.Open, msg.High, msg.Low, msg.Close, msg.PeriodVolume, true,
                    0.0, 0.0, 0, 0, false,
                    default(DateTime), 0.0, false);
                newBars.Add(newBar);
            }

            return newBars
                .Where(b => b.Time >= startTime && b.Time <= endTime)
                .OrderBy(b => b.Time);
        }
        #endregion

        #region public override string Name
        public override string Name
        {
            get
            {
                return "IQFeed";
            }
        }
        #endregion
    }
}

//==============================================================================
// end of file