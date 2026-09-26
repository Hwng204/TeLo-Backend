-- Migration: Add extended fields to schools table
-- Run this script if the schools table already exists but is missing these columns.
-- Safe to run multiple times (uses IF NOT EXISTS pattern via SHOW COLUMNS).

-- 1. Add 'type' column
ALTER TABLE `schools`
  ADD COLUMN IF NOT EXISTS `type` VARCHAR(100) NULL AFTER `status`;

-- 2. Add 'representative' column
ALTER TABLE `schools`
  ADD COLUMN IF NOT EXISTS `representative` VARCHAR(255) NULL AFTER `type`;

-- 3. Add 'contact_email' column
ALTER TABLE `schools`
  ADD COLUMN IF NOT EXISTS `contact_email` VARCHAR(255) NULL AFTER `representative`;

-- 4. Add 'contact_phone' column
ALTER TABLE `schools`
  ADD COLUMN IF NOT EXISTS `contact_phone` VARCHAR(50) NULL AFTER `contact_email`;

-- Verify result
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'schools' AND TABLE_SCHEMA = DATABASE()
ORDER BY ORDINAL_POSITION;
