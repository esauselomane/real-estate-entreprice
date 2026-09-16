# Google maps setup

Enable Maps Static API, Maps JavaScript API, Places API (New), and billing for the Google Cloud project containing your keys.

## Local frontend

Set `VITE_GOOGLE_MAPS_API_KEY` in `frontend-client/.env.local`, then restart Vite (or rebuild the production frontend). This is a browser-visible key: restrict it to Maps Static API, Maps JavaScript API and Places API (New), and your frontend HTTP referrers. Property Details shows a static map, with a link to open Google Maps. Missing/invalid coordinates or a failed image show a fallback.

## Brochure service

Set `GoogleMaps__ApiKey` in the CRM service process environment. For Docker Compose, a blank entry is provided in the root `.env` and passed to `crm-service`. Standalone dotnet does not automatically load `.env`; export the variable before starting CRM or supply it through your deployment's environment/secret configuration.

Use a separate server key restricted to Maps Static API and the server's outbound IP where stable. The brochure worker fetches the image server-side and embeds it in the PDF; the key is not included in the resulting PDF. A missing key produces an explicit map-unavailable section. Configured map download failures fail generation rather than silently producing an empty map.

`VITE_GOOGLE_MAPS_API_KEY` is also wired into the frontend Docker build arguments in docker-compose.prod.yml. Rebuild the frontend after changing it.

## Brochure map behavior

Distinct valid property coordinates become red markers. One blue dotted circle encloses the marker group, centred on its projected bounding box with 25% clearance. Co-located markers use a minimum enclosure radius. The circle is drawn in map projection coordinates so it appears circular; it is an overview boundary, not a measured travel radius. Google fits the markers and complete dotted path into the image. Attribution remains visible.

The map targets regional property portfolios. Portfolios straddling the antimeridian are not specially wrapped. Coordinates at (0,0), outside supported bounds or not finite are excluded. Maps with no valid points show an unavailable message.

References: [Maps Static API parameters](https://developers.google.com/maps/documentation/maps-static/start), [API security practices](https://developers.google.com/maps/api-security-best-practices).

## Property address selection

Property add/edit forms share a Google Places address picker, limited to South Africa. A selected result fills the formatted address and latitude/longitude. Editing the saved address manually clears coordinates so the previous location is not retained. Manual entry remains available if Google is unavailable. Existing address and coordinate columns are reused; no place ID is stored.

Enable Maps JavaScript API and Places API (New) for the browser key, in addition to Maps Static API. Allow the local and deployed frontend referrers. Verify suggestions and saving a selected address on staging before acceptance. Implementation follows the [Google Place Autocomplete widget](https://developers.google.com/maps/documentation/javascript/place-autocomplete-new).
