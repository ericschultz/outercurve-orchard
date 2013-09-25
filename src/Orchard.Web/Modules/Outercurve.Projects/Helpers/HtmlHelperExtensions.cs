﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using MarkdownSharp;
using Orchard.Localization;

namespace Outercurve.Projects.Helpers
{
    /// <summary>
    /// 
    /// </summary>
    /// <from>Signatory</from>
    public static class HtmlHelperExtensions
    {
        public static IHtmlString RenderMarkdown(this HtmlHelper htmlHelper, string markdown)
        {
            return htmlHelper.Raw(new Markdown().Transform(markdown));
        }


        public static MvcHtmlString LabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, LocalizedString labelText, object htmlAttributes = null)
        {
            return html.LabelFor(expression, labelText.ToString(), htmlAttributes);
        }

        public static MvcHtmlString LabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, string labelText, object htmlAttributes = null)
        {
            if (String.IsNullOrEmpty(labelText))
            {
                return MvcHtmlString.Empty;
            }
            var htmlFieldName = ExpressionHelper.GetExpressionText(expression);
            var tag = new TagBuilder("label");
            tag.Attributes.Add("for", html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldId(htmlFieldName));
            tag.SetInnerText(labelText);
            if (htmlAttributes != null) {
                tag.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            }
            return MvcHtmlString.Create(tag.ToString(TagRenderMode.Normal));
        }

        public static MvcHtmlString ControlLabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, LocalizedString labelText)
        {
            return ControlLabelFor(html, expression, labelText, null);
        }


        public static MvcHtmlString ControlLabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, LocalizedString labelText, object htmlAttributes) {
            return ControlLabelFor(html, expression, labelText.ToString(), htmlAttributes);

        }

        public static MvcHtmlString ControlLabelFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, string labelText, object htmlAttributes)
        {
            if (String.IsNullOrEmpty(labelText))
            {
                return MvcHtmlString.Empty;
            }

            var htmlFieldName = ExpressionHelper.GetExpressionText(expression);
            var isRequired = html.IsRequired(expression);
            var tag = new TagBuilder("label");
            tag.Attributes.Add("for", html.ViewContext.ViewData.TemplateInfo.GetFullHtmlFieldId(htmlFieldName));
            
            
            if (htmlAttributes != null)
            {
                tag.MergeAttributes(new RouteValueDictionary(htmlAttributes));
            }

            if (isRequired) {
                tag.SetInnerText(labelText + " *");
                tag.MergeAttribute("class", "control-label for-required-field");
            }
            else {
                tag.SetInnerText(labelText);
                tag.MergeAttribute("class", "control-label");
            }

            return MvcHtmlString.Create(tag.ToString(TagRenderMode.Normal));
            
        }

        public static MvcHtmlString ControlValidationMessageFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression, string validationMessage) {
            return html.ValidationMessageFor(expression, validationMessage, new {@class = "help-inline"});
        }


        public static MvcHtmlString ControlTextBoxFor<TModel, TValue>(this HtmlHelper<TModel> html, Expression<Func<TModel, TValue>> expression) {
            IDictionary<string,object> attributes = new Dictionary<string, object>();
            
            if (html.IsRequired(expression)) {
                attributes["required"] = "required";
            }

            return html.TextBoxFor(expression, attributes);
        }
    }
}