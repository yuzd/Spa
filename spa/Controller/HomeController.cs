using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using spa.Filter;
using spa.JavaScriptViewEngine.Utils;
using spa.Models;

namespace spa.Controller
{
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {

        public IActionResult Index()
        {
            return View("js-{auto}");
        }

    }
}