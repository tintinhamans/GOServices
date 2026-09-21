/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using System.Diagnostics;
using OpenTelemetry;

namespace GenOnlineService
{
    // Removes secrets and optional SQL from database spans.
    public sealed class DatabaseActivitySanitizer : BaseProcessor<Activity>
    {
        private readonly bool _includeDatabaseStatements;

        public DatabaseActivitySanitizer(bool includeDatabaseStatements)
        {
            _includeDatabaseStatements = includeDatabaseStatements;
        }

        public override void OnEnd(Activity activity)
        {
            if (!String.Equals(activity.Source.Name, AppMetrics.MySqlActivitySourceName, StringComparison.Ordinal))
            {
                return;
            }

            activity.SetTag("db.connection_string", null);
            activity.SetTag("db.user", null);

            if (!_includeDatabaseStatements)
            {
                activity.SetTag("db.statement", null);
                activity.SetTag("db.query.text", null);
            }
        }
    }
}
