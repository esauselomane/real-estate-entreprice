import React, { useEffect, useState } from 'react';
import { Building2, UserPlus, ShieldCheck, Users, Plus } from 'lucide-react';
import { crmService } from '../services/ crmService.js';

export default function SystemAdminWorkspace() {
  const [agencies, setAgencies] = useState([]);
  const [brokers, setBrokers] = useState([]);
  const [agencyForm, setAgencyForm] = useState({ name: '', contactEmail: '', headquartersAddress: '' });
  const [brokerForm, setBrokerForm] = useState({ fullName: '', email: '', phone: '', agencyId: '', role: 'Broker' });
  const [error, setError] = useState('');

  const loadData = async () => {
    try {
      const [agencyRows, brokerRows] = await Promise.all([
        crmService.getAgencies(),
        crmService.getBrokers()
      ]);
      setAgencies(agencyRows || []);
      setBrokers(brokerRows || []);
      if (!brokerForm.agencyId && (agencyRows || []).length) {
        setBrokerForm((prev) => ({ ...prev, agencyId: (agencyRows[0].agencyId || agencyRows[0].tenantId || '') }));
      }
    } catch (err) {
      setError(err || 'Unable to load agency records.');
    }
  };

  useEffect(() => { loadData(); }, []);

  const handleAgencySubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.createAgency(agencyForm);
      setAgencyForm({ name: '', contactEmail: '', headquartersAddress: '' });
      await loadData();
    } catch (err) {
      alert(err || 'Agency creation failed.');
    }
  };

  const handleBrokerSubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.createBroker(brokerForm);
      setBrokerForm((prev) => ({ ...prev, fullName: '', email: '', phone: '', role: 'Broker' }));
      await loadData();
    } catch (err) {
      alert(err || 'Broker onboarding failed.');
    }
  };

  return (
    <div className="space-y-6 animate-fade-in">
      {error && <div className="p-3 rounded-xl border border-red-900 bg-red-950/40 text-red-300 text-xs">{error}</div>}

      <div className="grid gap-6 xl:grid-cols-2">
        <form onSubmit={handleAgencySubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><Building2 size={14} className="text-blue-500" /> Add Agency</div>
          <input value={agencyForm.name} onChange={(e) => setAgencyForm({ ...agencyForm, name: e.target.value })} placeholder="Agency name" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
          <input value={agencyForm.contactEmail} onChange={(e) => setAgencyForm({ ...agencyForm, contactEmail: e.target.value })} type="email" placeholder="Contact email" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
          <input value={agencyForm.headquartersAddress} onChange={(e) => setAgencyForm({ ...agencyForm, headquartersAddress: e.target.value })} placeholder="Headquarters address" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
          <button type="submit" className="w-full py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold">Provision agency</button>
        </form>

        <form onSubmit={handleBrokerSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px]"><UserPlus size={14} className="text-emerald-500" /> Add Broker</div>
          <input value={brokerForm.fullName} onChange={(e) => setBrokerForm({ ...brokerForm, fullName: e.target.value })} placeholder="Broker full name" className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
          <div className="grid grid-cols-2 gap-3">
            <input value={brokerForm.email} onChange={(e) => setBrokerForm({ ...brokerForm, email: e.target.value })} type="email" placeholder="Email" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" required />
            <input value={brokerForm.phone} onChange={(e) => setBrokerForm({ ...brokerForm, phone: e.target.value })} placeholder="Phone" className="bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white" />
          </div>
          <select value={brokerForm.agencyId} onChange={(e) => setBrokerForm({ ...brokerForm, agencyId: e.target.value })} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
            {agencies.map((agency) => <option key={agency.agencyId || agency.tenantId} value={agency.agencyId || agency.tenantId}>{agency.name}</option>)}
          </select>
          <select value={brokerForm.role} onChange={(e) => setBrokerForm({ ...brokerForm, role: e.target.value })} className="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
            <option value="Broker">Broker</option>
            <option value="Manager">Manager</option>
            <option value="Agent">Agent</option>
          </select>
          <button type="submit" className="w-full py-2.5 rounded-xl bg-emerald-600 text-white text-xs font-bold">Create broker profile</button>
        </form>
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Building2 size={14} className="text-blue-500" /> Agency roster</div>
          <div className="space-y-3">
            {agencies.map((agency) => (
              <div key={agency.agencyId || agency.tenantId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-3">
                <div className="flex justify-between items-center">
                  <span className="text-white font-bold text-sm">{agency.name}</span>
                  <span className="text-[10px] px-2 py-1 rounded-md bg-emerald-950 text-emerald-300 border border-emerald-900">{agency.operationalStatus || 'Active'}</span>
                </div>
                <p className="text-xs text-slate-400 mt-1">{agency.contactEmail}</p>
                <p className="text-[11px] text-slate-500 mt-1">{agency.headquartersAddress || 'No address provided'}</p>
              </div>
            ))}
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5">
          <div className="flex items-center gap-2 text-white font-bold uppercase tracking-[0.2em] text-[10px] mb-4"><Users size={14} className="text-violet-500" /> Broker roster</div>
          <div className="space-y-3">
            {brokers.map((broker) => (
              <div key={broker.brokerId} className="rounded-xl border border-slate-800 bg-slate-950/60 p-3">
                <div className="flex justify-between items-center">
                  <span className="text-white font-bold text-sm">{broker.fullName}</span>
                  <span className="text-[10px] px-2 py-1 rounded-md bg-violet-950 text-violet-300 border border-violet-900">{broker.role}</span>
                </div>
                <p className="text-xs text-slate-400 mt-1">{broker.email} • {broker.phone || 'No phone'}</p>
                <p className="text-[11px] text-slate-500 mt-1">Agency: {broker.agencyId}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
