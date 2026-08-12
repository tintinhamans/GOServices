/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using GenOnlineService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PendingMatchUpload
{
	public string UploadId { get; set; } = String.Empty;
	public byte[] TokenHash { get; set; } = Array.Empty<byte>();
	public long UserId { get; set; }
	public long MatchId { get; set; }
	public int SlotIndex { get; set; }
	public string Bucket { get; set; } = String.Empty;
	public string ObjectKey { get; set; } = String.Empty;
	public string FileName { get; set; } = String.Empty;
	public EMetadataFileType FileType { get; set; }
	public DateTime CreatedUtc { get; set; }
	public DateTime UploadExpiresUtc { get; set; }
	public DateTime ConfirmationExpiresUtc { get; set; }
	public DateTime NextCheckUtc { get; set; }
	public int CheckAttempts { get; set; }
	public DateTime? ConfirmedUtc { get; set; }
}

public sealed class PendingMatchUploadConfiguration : IEntityTypeConfiguration<PendingMatchUpload>
{
	public void Configure(EntityTypeBuilder<PendingMatchUpload> entity)
	{
		entity.ToTable("pending_match_uploads");
		entity.HasKey(upload => upload.UploadId);

		entity.Property(upload => upload.UploadId).HasColumnName("upload_id").HasMaxLength(32);
		entity.Property(upload => upload.TokenHash).HasColumnName("token_hash").HasColumnType("binary(32)").IsRequired();
		entity.Property(upload => upload.UserId).HasColumnName("user_id");
		entity.Property(upload => upload.MatchId).HasColumnName("match_id");
		entity.Property(upload => upload.SlotIndex).HasColumnName("slot_index");
		entity.Property(upload => upload.Bucket).HasColumnName("bucket").HasMaxLength(255);
		entity.Property(upload => upload.ObjectKey).HasColumnName("object_key").HasMaxLength(1024).HasCharSet("ascii");
		entity.Property(upload => upload.FileName).HasColumnName("file_name").HasMaxLength(255);
		entity.Property(upload => upload.FileType).HasColumnName("file_type");
		entity.Property(upload => upload.CreatedUtc).HasColumnName("created_utc").HasColumnType("datetime(6)");
		entity.Property(upload => upload.UploadExpiresUtc).HasColumnName("upload_expires_utc").HasColumnType("datetime(6)");
		entity.Property(upload => upload.ConfirmationExpiresUtc).HasColumnName("confirmation_expires_utc").HasColumnType("datetime(6)");
		entity.Property(upload => upload.NextCheckUtc).HasColumnName("next_check_utc").HasColumnType("datetime(6)");
		entity.Property(upload => upload.CheckAttempts).HasColumnName("check_attempts");
		entity.Property(upload => upload.ConfirmedUtc).HasColumnName("confirmed_utc").HasColumnType("datetime(6)");

		entity.HasIndex(upload => new { upload.ConfirmedUtc, upload.NextCheckUtc });
		entity.HasIndex(upload => upload.ConfirmationExpiresUtc);
		entity.HasIndex(upload => upload.ObjectKey).IsUnique();
	}
}
