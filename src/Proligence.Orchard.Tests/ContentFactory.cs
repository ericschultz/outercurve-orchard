﻿namespace Proligence.Orchard.Testing
{
    using global::Orchard.ContentManagement;
    using global::Orchard.ContentManagement.Records;

    public static class ContentFactory
    {
        public static ContentItem CreateContentItem(int id, string contentType, params ContentPart[] parts)
        {
            var item = new ContentItem
            {
                VersionRecord = new ContentItemVersionRecord
                {
                    ContentItemRecord = new ContentItemRecord { Id = id }
                }
            };

            if (contentType != null)
            {
                item.VersionRecord.ContentItemRecord.ContentType = new ContentTypeRecord { Name = contentType };
                item.ContentType = contentType;
            }

            foreach (ContentPart part in parts) {
                
                item.Weld(part);
            }

            return item;
        }

        public static ContentItem CreateContentItem(int id, params ContentPart[] parts)
        {
            return CreateContentItem(id, null, parts);
        }

        public static ContentItem CreateContentItem(params ContentPart[] parts)
        {
            return CreateContentItem(0, null, parts);
        }
    }
}