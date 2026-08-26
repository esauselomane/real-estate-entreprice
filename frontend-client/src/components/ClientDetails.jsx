import React from 'react';
import { ArrowLeft, User, Phone, Mail, DollarSign, Map, Landmark } from 'lucide-react';

export default function ClientDetails({ clientId, clientType = 'Client', clientName = 'Client Profile', onBack }) {
  // Mock profiling records matching spatial data filters criteria 
  const safeName = clientName || 'Client Profile';
  const emailPrefix = safeName.toLowerCase().replace(/\s+/g, '');
  const clientProfile = {
    id: clientId || 'N/A',
    name: safeName,
    type: clientType || 'Client',
    phone: "+27 (0) 11 555 9832",
    email: `${emailPrefix}@example.com`,
    budgetCeiling: clientType === 'Owner' ? 12000000 : 80000,
    preferredAreas: ["Green Point", "Sea Point", "Sandton Hub"],
    financialHistory: ["Tax_Clearance_2026.pdf", "FICA_Verification.pdf"]
  };

  return (
    <div className="space-y-6 animate-fade-in">
      
      {/* 🧭 Navigation Header */}
      <div className="flex items-center justify-between">
        <button 
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-semibold text-slate-400 hover:text-white bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl transition-colors cursor-pointer"
        >
          <ArrowLeft size={14} /> Back to Asset Details
        </button>
        <span className="text-xs font-mono text-slate-500">Profile Database Index: {clientId}</span>
      </div>

      {/* Profile Overview Card Layout */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
        <div className="flex items-center gap-4 mb-6">
          <div className={`w-14 h-14 rounded-full flex items-center justify-center font-bold text-white shadow-md ${
            clientType === 'Owner' ? 'bg-gradient-to-tr from-amber-600 to-orange-600' : 'bg-gradient-to-tr from-blue-600 to-indigo-600'
          }`}>
            <User size={24} />
          </div>
          <div>
            <h3 className="text-xl font-bold text-white tracking-tight">{clientProfile.name}</h3>
            <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded ${
              clientType === 'Owner' ? 'bg-amber-950 text-amber-400 border border-amber-900' : 'bg-blue-950 text-blue-400 border border-blue-900'
            }`}>
              System Entity Class: {clientProfile.type}
            </span>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-4 border-t border-slate-800/60">
          
          {/* Contact Information Form Parameters */}
          <div className="space-y-4">
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">Contact Records Information</h4>
            <div className="space-y-2">
              <div className="flex items-center gap-3 text-sm text-slate-300">
                <Phone size={14} className="text-slate-500" /> {clientProfile.phone}
              </div>
              <div className="flex items-center gap-3 text-sm text-slate-300">
                <Mail size={14} className="text-slate-500" /> {clientProfile.email}
              </div>
            </div>
          </div>

          {/* Budgeting Matrices & Filtering Criteria Parameters */}
          <div className="space-y-4">
            <h4 className="text-xs font-bold text-slate-400 uppercase tracking-wider">Agent Allocation Matching Limits</h4>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-slate-300">
                <DollarSign size={14} className="text-emerald-500" /> 
                <span className="font-semibold text-white">
                  Budget Threshold Limit: R {clientProfile.budgetCeiling.toLocaleString()} {clientType === 'Tenant' ? '/month' : ''}
                </span>
              </div>
              <div className="flex items-center gap-2 text-sm text-slate-300">
                <Map size={14} className="text-blue-500" />
                <span className="font-semibold text-slate-400">Preferred Target Areas: </span>
                <span className="text-white font-medium text-xs bg-slate-950 px-2 py-1 rounded-md border border-slate-800">
                  {clientProfile.preferredAreas.join(', ')}
                </span>
              </div>
            </div>
          </div>

        </div>
      </div>

      {/* Legal & KYC Attestations Section */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl">
        <h4 className="text-sm font-bold text-white uppercase tracking-wider mb-4 flex items-center gap-2">
          <Landmark size={16} className="text-blue-500" /> Regulatory Compliance Documents (FICA/KYC)
        </h4>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {clientProfile.financialHistory.map((historyFile, idx) => (
            <div key={idx} className="p-3 bg-slate-950 border border-slate-800 rounded-xl text-xs text-slate-300 flex items-center gap-3">
              <div className="w-2 h-2 rounded-full bg-emerald-500" />
              <span>{historyFile} Verified</span>
            </div>
          ))}
        </div>
      </div>

    </div>
  );
}