//-----------------------------------------------------------------------
// <copyright file="SpaModel .cs" company="Company">
// Copyright (C) Company. All Rights Reserved.
// </copyright>
// <author>nainaigu</author>
// <create>$Date$</create>
// <summary></summary>
//-----------------------------------------------------------------------

namespace spa.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// 
    /// </summary>
    public class SpaModel
    {

        public string Title { get; set; }
        public string Time { get; set; }
        public DateTime DateTime { get; set; }
        public string Rollback { get; set; }
        public string RollbackPath {
            get
            {
                if (string.IsNullOrEmpty(Rollback)) return string.Empty;
                return Title + "-" + Rollback;
            }
        }
    }
}