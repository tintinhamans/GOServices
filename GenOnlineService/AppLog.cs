/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GenOnlineService
{
    // Logger for types without constructor injection.
    public static class AppLog
    {
        public static ILogger For(Type category)
        {
            return ServiceLocator.Services.GetRequiredService<ILoggerFactory>().CreateLogger(category.FullName ?? category.Name);
        }

        public static ILogger For<T>()
        {
            return For(typeof(T));
        }
    }
}
