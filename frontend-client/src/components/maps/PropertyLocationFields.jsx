const fields = [['countryCode', 'Country code (ZA, GB, US…)'], ['country', 'Country'], ['region', 'Province / state / region'], ['district', 'District / county'], ['city', 'City / town'], ['suburb', 'Suburb / neighbourhood']];

export default function PropertyLocationFields({ value, onChange }) {
  function updateField(event) {
    const next = { countryCode: '', country: '', region: '', district: '', city: '', suburb: '', ...value, [event.target.name]: event.target.value };
    onChange(next);
  }
  return <fieldset className="col-span-full grid grid-cols-2 gap-3 rounded-xl border border-slate-700 p-3">
    <legend className="px-2 text-xs text-slate-300">Confirm location details</legend>
    <p className="col-span-full text-xs text-slate-400">Check the details supplied by Google. Leave administrative levels blank where they do not apply. Confirm a country code to enable precise area searches.</p>
    {fields.map(([name, label]) => <label key={name} className="text-xs text-slate-400">{label}<input name={name} value={value?.[name] || ''} onChange={updateField} maxLength={name === 'countryCode' ? 2 : 150} pattern={name === 'countryCode' ? '[A-Za-z]{2}' : undefined} className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-white" /></label>)}
  </fieldset>;
}
