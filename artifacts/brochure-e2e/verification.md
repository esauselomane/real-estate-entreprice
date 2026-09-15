# Brochure API-to-PDF verification

- Local Identity login and Property API reads succeeded.
- Generated through POST `/api/crm/brochure/generate` on an isolated CRM instance with SMTP disabled.
- Deal: 1007. Agency: Agency 2. Broker: Broker 2.
- Property: 1 Oak Street, Johannesburg, South Africa (property 1).
- Included units: Unit 1 and Unit 3, using the property's actual uploaded gallery image.
- Randomly sampled seeded properties had placeholder filenames rather than usable photographs; the photographed property was selected instead. This is not a multi-property/multi-marker visual test.
- Google Maps accepted the configured server key. The PDF map includes the marker and centred dotted enclosure, with attribution visible.
- PDF: `Brochure_Deal_1007.pdf`, four pages. Map and unit-photo pages visually inspected.
- First run produced the PDF but hit a legacy database schema mismatch during post-processing. Added and applied migration `20260915000100_RepairBrochureBrandingColumns`, then reran successfully. Final worker log: `Completed DealId=1007 Success=True`.
- No email sent. Existing user service processes were left untouched. This test exercises the generation API and worker; it does not certify the browser's draft-card/download workflow (brochureCardId was 0).
