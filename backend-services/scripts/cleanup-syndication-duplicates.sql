-- ============================================
-- SQL SCRIPT: Clean up duplicate syndication records
-- Run this against the Property database
-- ============================================

-- 1. Create a staging table with unique (PropertyId, PlatformId) combinations
-- Keeping the row with the lowest PropertyPlatformSyndicationId (earliest created)
WITH Duplicates AS (
    SELECT 
        PropertyPlatformSyndicationId,
        PropertyId,
        PlatformId,
        ROW_NUMBER() OVER (
            PARTITION BY PropertyId, PlatformId 
            ORDER BY PropertyPlatformSyndicationId ASC
        ) AS rn
    FROM PropertyPlatformSyndications
)
DELETE FROM PropertyPlatformSyndications
WHERE PropertyPlatformSyndicationId IN (
    SELECT PropertyPlatformSyndicationId 
    FROM Duplicates 
    WHERE rn > 1
);

-- 2. Verify the cleanup
SELECT 
    PropertyId, 
    PlatformId, 
    COUNT(*) as RecordCount
FROM PropertyPlatformSyndications
GROUP BY PropertyId, PlatformId
HAVING COUNT(*) > 1;

-- 3. If the above returns no rows, duplicates have been cleaned.
-- 4. The unique constraint will be added automatically on next service startup via Program.cs
