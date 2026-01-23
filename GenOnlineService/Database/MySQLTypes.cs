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

using System;
using System.Collections.Generic;
using Dimension = System.UInt32;
using EntityDatabaseID = System.Int64;

public class CMySQLRow
{
	public CMySQLRow()
	{

	}

	public T? GetValue<T>(string strKey)
	{
		return (T?)Convert.ChangeType(m_Fields[strKey], typeof(T?));
	}

	public Dictionary<string, object?> GetFields()
	{
		return m_Fields;
	}

	public object? this[string strKey]
	{
		get => m_Fields[strKey];
		set => m_Fields[strKey] = value;
	}

	private readonly Dictionary<string, object?> m_Fields = new Dictionary<string, object?>();
}