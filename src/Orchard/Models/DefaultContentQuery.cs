using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Impl;
using NHibernate.Linq;
using Orchard.Data;
using Orchard.Models.Records;

namespace Orchard.Models {
    public class DefaultContentQuery : IContentQuery {
        private readonly ISessionLocator _sessionLocator;
        private ISession _session;
        private ICriteria _itemCriteria;

        public DefaultContentQuery(IContentManager contentManager, ISessionLocator sessionLocator) {
            _sessionLocator = sessionLocator;
            ContentManager = contentManager;
        }

        public IContentManager ContentManager { get; private set; }

        ISession BindSession() {
            if (_session == null)
                _session = _sessionLocator.For(typeof(ContentItemRecord));
            return _session;
        }

        ICriteria BindItemCriteria() {
            if (_itemCriteria == null) {
                _itemCriteria = BindSession().CreateCriteria<ContentItemRecord>();
            }
            return _itemCriteria;
        }

        ICriteria BindCriteriaByPath(string path) {
            var itemCriteria = BindItemCriteria();
            return itemCriteria.GetCriteriaByPath(path) ?? itemCriteria.CreateCriteria(path);
        }


        public IContentQuery ForType(params string[] contentTypeNames) {
            BindCriteriaByPath("ContentType").Add(Restrictions.InG("Name", contentTypeNames));
            return this;
        }


        public IContentQuery Where<TRecord>() {
            // this simply demands an inner join
            BindCriteriaByPath(typeof(TRecord).Name);
            return this;
        }

        public IContentQuery Where<TRecord>(Expression<Func<TRecord, bool>> predicate) {

            // build a linq to nhibernate expression
            var options = new QueryOptions();
            var queryProvider = new NHibernateQueryProvider(BindSession(), options);
            var queryable = new Query<TRecord>(queryProvider, options).Where(predicate);

            // translate it into the nhibernate ICriteria implementation
            var criteria = (CriteriaImpl)queryProvider.TranslateExpression(queryable.Expression);

            // attach the criterion from the predicate to this query's criteria for the record
            var recordCriteria = BindCriteriaByPath(typeof(TRecord).Name);
            foreach (var entry in criteria.IterateExpressionEntries()) {
                recordCriteria.Add(entry.Criterion);
            }

            return this;
        }


        public IEnumerable<ContentItem> Select() {
            return BindItemCriteria()
                .List<ContentItemRecord>()
                .Select(x => ContentManager.Get(x.Id));
        }
    }
}