import { useState, useEffect, useRef } from 'react';
import { DollarSign, TrendingUp, UserPlus, User, Loader2, Building2, Edit2, Search } from 'lucide-react';
import { crmService } from '../services/crmService.js';
import { usePermission } from '../contexts/PermissionContext.jsx';
import Can from '../components/Can.jsx';

export default function CRMWorkspace() {
  const { hasPermission } = usePermission();

  const [activeTab, setActiveTab] = useState('contacts');

  const [companies, setCompanies] = useState([]);
  const [companiesTotalCount, setCompaniesTotalCount] = useState(0);
  const [legalEntities, setLegalEntities] = useState([]);
  const [legalEntitiesTotalCount, setLegalEntitiesTotalCount] = useState(0);
  const [contacts, setContacts] = useState([]);
  const [contactsTotalCount, setContactsTotalCount] = useState(0);

  const [summaryReport, setSummaryReport] = useState({
    activeLeadsCount: 0, valuationsInProcessCount: 0, conveyancingAtDeedsCount: 0, finalizedClosedCount: 0, totalPipelineEstimatedValue: 0
  });

  const [editingCompany, setEditingCompany] = useState(null);
  const [editingLegalEntity, setEditingLegalEntity] = useState(null);
  const [editingContact, setEditingContact] = useState(null);
  const [isContactFormOpen, setIsContactFormOpen] = useState(false);
  const [contactForm, setContactForm] = useState({
    fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '',
    contactRoleType: 'Buyer', associatedCompanyId: '', associatedLegalEntityId: '',
    linkedDealIds: '', area: '', budget: ''
  });

  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');

  const loadActiveWorkspaceData = async () => {
    setIsLoading(true);
    setErrorMessage('');
    try {
      const metrics = await crmService.getPipelineReportSummary();
      setSummaryReport(metrics);

      const [cardsRes] = await Promise.all([
        crmService.getActiveBrochureCards().catch(() => [])
      ]);

      if (activeTab === 'companies') {
        const res = await crmService.getCompanies();
        setCompanies((res || []).items || (res || []));
        setCompaniesTotalCount((res || []).totalCount || 0);
      } else if (activeTab === 'legalEntities') {
        const res = await crmService.getLegalEntities();
        setLegalEntities((res || []).items || (res || []));
        setLegalEntitiesTotalCount((res || []).totalCount || 0);
      } else if (activeTab === 'contacts') {
        const res = await crmService.getContacts();
        setContacts((res || []).items || (res || []));
        setContactsTotalCount((res || []).totalCount || 0);
      }
    } catch (err) {
      setErrorMessage(err || 'Failed to populate operational ledger streams.');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadActiveWorkspaceData();
  }, [activeTab]);

  const PAGE_SIZE = 20;
  const [searchTerm, setSearchTerm] = useState('');
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  const handleSetTab = (id) => {
    setActiveTab(id);
    setSearchTerm('');
    setVisibleCount(PAGE_SIZE);
  };

  const sentinelRef = useRef(null);
  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;
    const observer = new IntersectionObserver((entries) => {
      if (entries[0].isIntersecting) setVisibleCount((c) => c + PAGE_SIZE);
    }, { threshold: 1 });
    observer.observe(el);
    return () => observer.disconnect();
  }, [sentinelRef]);

  const filterBySearch = (items, fieldFns) => {
    const term = searchTerm.trim().toLowerCase();
    if (!term) return items;
    return items.filter((item) =>
      fieldFns.some((fn) => String(fn(item) ?? '').toLowerCase().includes(term))
    );
  };
  const firstVisible = (items, fieldFns) => filterBySearch(items, fieldFns).slice(0, visibleCount);

  const handleUpdateCompanySubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateCompany(editingCompany);
      setEditingCompany(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply company updates.');
    }
  };

  const handleUpdateLegalEntitySubmit = async (e) => {
    e.preventDefault();
    try {
      await crmService.updateLegalEntity(editingLegalEntity);
      setEditingLegalEntity(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to apply legal entity updates.');
    }
  };

  const handleUpdateContactSubmit = async (e) => {
    e.preventDefault();
    if (!editingContact) return;
    try {
      await crmService.updateContact({
        ...editingContact,
        linkedDealIds: editingContact.linkedDealIds || ''
      });
      setEditingContact(null);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to update contact.');
    }
  };

  const handleCreateContact = async (e) => {
    e.preventDefault();
    try {
      await crmService.createContact({
        ...contactForm,
        linkedDealIds: contactForm.linkedDealIds || ''
      });
      setContactForm({
        fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '',
        contactRoleType: 'Buyer', associatedCompanyId: '', associatedLegalEntityId: '',
        linkedDealIds: '', area: '', budget: ''
      });
      setIsContactFormOpen(false);
      await loadActiveWorkspaceData();
    } catch (err) {
      alert(err || 'Failed to create contact.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Unverified Leads</span>
            <span className="text-2xl font-black text-white mt-1 block">{summaryReport.activeLeadsCount} Active</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-blue-950 border border-blue-900 flex items-center justify-center text-blue-400">
            <UserPlus size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Active Deals</span>
            <span className="text-2xl font-black text-amber-400 mt-1 block">{summaryReport.valuationsInProcessCount} Open</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-amber-950/40 border border-amber-900 flex items-center justify-center text-amber-400">
            <TrendingUp size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">In Negotiation</span>
            <span className="text-2xl font-black text-purple-400 mt-1 block">{summaryReport.conveyancingAtDeedsCount} Reviewed</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-purple-950/40 border border-purple-900 flex items-center justify-center text-purple-400">
            <TrendingUp size={18} />
          </div>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl flex items-center justify-between group">
          <div>
            <span className="block text-[10px] text-slate-500 font-bold uppercase tracking-wider">Closed Won Value</span>
            <span className="text-2xl font-black text-emerald-400 mt-1 block">R {summaryReport.totalPipelineEstimatedValue.toLocaleString()}</span>
          </div>
          <div className="w-10 h-10 rounded-xl bg-emerald-950/40 border border-emerald-900 flex items-center justify-center text-emerald-400">
            <DollarSign size={18} />
          </div>
        </div>
      </div>

      <div className="flex bg-slate-900 border border-slate-800 p-1.5 rounded-2xl gap-1.5 max-w-4xl shadow-xl overflow-x-auto scrollbar-none">
        {[
          { id: 'companies', label: 'Companies Directory', count: companiesTotalCount },
          { id: 'legalEntities', label: 'Legal Entities', count: legalEntitiesTotalCount },
          { id: 'contacts', label: 'Human Contacts', count: contactsTotalCount }
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => handleSetTab(tab.id)}
            type="button"
            className={`text-xs font-bold py-2.5 px-4 rounded-xl transition-all cursor-pointer whitespace-nowrap ${
              activeTab === tab.id ? 'bg-blue-600 text-white shadow-lg' : 'text-slate-400 hover:text-white hover:bg-slate-800/40'
            }`}
          >
            {tab.label} {tab.count > 0 && `(${tab.count})`}
          </button>
        ))}
      </div>

      {errorMessage && (
        <div className="p-4 bg-red-950/30 border border-red-900/60 text-red-400 rounded-2xl text-xs font-medium text-center">
          {errorMessage}
        </div>
      )}

      {!isLoading && (
        <div className="relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
          <input
            type="text"
            placeholder="Search this list..."
            value={searchTerm}
            onChange={(e) => { setSearchTerm(e.target.value); setVisibleCount(PAGE_SIZE); }}
            className="w-full pl-9 pr-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white placeholder-slate-500 focus:outline-none focus:border-blue-500"
          />
        </div>
      )}

      {isLoading ? (
        <div className="py-20 flex flex-col items-center justify-center text-slate-500 gap-3">
          <Loader2 className="w-8 h-8 text-blue-500 animate-spin" />
          <span className="text-xs font-medium tracking-wide">Syncing real-time corporate relationship tables...</span>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl">
            <Can permission="users:create">
              <button
                type="button"
                onClick={() => {
                  setIsContactFormOpen(!isContactFormOpen);
                  if (!isContactFormOpen) {
                    setContactForm({
                      fullName: '', emailAddress: '', phoneNumber: '', positionJobTitle: '',
                      contactRoleType: 'Buyer', associatedCompanyId: '', associatedLegalEntityId: '',
                      linkedDealIds: '', area: '', budget: ''
                    });
                  }
                }}
                className="flex items-center gap-2 text-xs font-bold text-slate-300 hover:text-white cursor-pointer"
              >
                <UserPlus size={14} /> New Contact {isContactFormOpen ? '▲' : '▼'}
              </button>
            </Can>
            {isContactFormOpen && (
              <form onSubmit={handleCreateContact} className="mt-4 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 animate-fade-in">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Full Name</label><input required type="text" value={contactForm.fullName} onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Email</label><input required type="email" value={contactForm.emailAddress} onChange={(e) => setContactForm({ ...contactForm, emailAddress: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Phone</label><input type="text" value={contactForm.phoneNumber} onChange={(e) => setContactForm({ ...contactForm, phoneNumber: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Job Title</label><input type="text" value={contactForm.positionJobTitle} onChange={(e) => setContactForm({ ...contactForm, positionJobTitle: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Role Type</label><select value={contactForm.contactRoleType} onChange={(e) => setContactForm({ ...contactForm, contactRoleType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"><option>Buyer</option><option>Seller</option><option>Broker</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Preferred Area</label><input type="text" placeholder="e.g. Sandton, Cape Town" value={contactForm.area} onChange={(e) => setContactForm({ ...contactForm, area: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Budget</label><input type="text" placeholder="e.g. R 1.5M - 2M" value={contactForm.budget} onChange={(e) => setContactForm({ ...contactForm, budget: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Linked Deal IDs (CSV)</label><input type="text" placeholder="e.g. 12,15" value={contactForm.linkedDealIds} onChange={(e) => setContactForm({ ...contactForm, linkedDealIds: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="lg:col-span-3 flex gap-3 pt-1"><button type="submit" className="py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl cursor-pointer">Create Contact</button><button type="button" onClick={() => setIsContactFormOpen(false)} className="py-2 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">Cancel</button></div>
              </form>
            )}
          </div>

          {activeTab === 'companies' && firstVisible(companies, [
            (c) => c.businessName, (c) => c.registrationNumber, (c) => c.vatNumber, (c) => c.industryVertical
          ]).map((comp) => (
            <div key={comp.companyId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight flex items-center gap-2"><Building2 size={16} className="text-slate-500" /> {comp.businessName}</h4>
                <p className="text-xs text-slate-400 mt-1">Reg: {comp.registrationNumber || 'N/A'} • VAT: {comp.vatNumber || 'N/A'} • Credit Limit: R {comp.corporateCreditLimit.toLocaleString()}</p>
                <p className="text-[11px] text-slate-500 mt-0.5">HQ: {comp.headOfficeAddress || 'Not Provided'}</p>
              </div>
              <Can permission="agencies:edit">
                <button onClick={() => setEditingCompany(comp)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
              </Can>
            </div>
          ))}

          {activeTab === 'legalEntities' && firstVisible(legalEntities, [
            (e) => e.registeredLegalName, (e) => e.entityTypeClassifier, (e) => e.taxRegistrationIndex, (e) => e.deedsOfficeIdentifier
          ]).map((ent) => (
            <div key={ent.legalEntityId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight">{ent.registeredLegalName}</h4>
                <p className="text-xs text-slate-400 mt-1">Structure: <span className="text-blue-400">{ent.entityTypeClassifier}</span> • Tax Ref: {ent.taxRegistrationIndex || 'N/A'} • Deeds Reg: {ent.deedsOfficeIdentifier || 'N/A'}</p>
                {ent.companyDetails && <p className="text-[11px] text-slate-500 mt-0.5">Belongs To Company: <span className="text-slate-400 font-semibold">{ent.companyDetails.businessName}</span></p>}
              </div>
              <Can permission="agencies:edit">
                <button onClick={() => setEditingLegalEntity(ent)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
              </Can>
            </div>
          ))}

          {activeTab === 'contacts' && firstVisible(contacts, [
            (c) => c.fullName, (c) => c.contactRoleType, (c) => c.positionJobTitle,
            (c) => c.emailAddress, (c) => c.phoneNumber,
            (c) => c.area, (c) => c.budget
          ]).map((con) => (
            <div key={con.contactId} className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex justify-between items-center hover:border-slate-700 transition-colors">
              <div>
                <h4 className="text-white font-bold text-base tracking-tight">{con.fullName}</h4>
                <p className="text-xs text-slate-400 mt-1">Role: <span className="text-purple-400 font-bold">{con.contactRoleType}</span> ({con.positionJobTitle}) • Email: {con.emailAddress} • Phone: {con.phoneNumber}</p>
                {(con.area || con.budget) && (
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    Area: <span className="text-slate-400">{con.area || 'N/A'}</span> • Budget: <span className="text-slate-400">{con.budget || 'N/A'}</span>
                  </p>
                )}
                {(con.companyDetails || con.legalEntityDetails) && (
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    Affiliation: {con.companyDetails?.businessName || ''} {con.legalEntityDetails?.registeredLegalName ? `[Entity: ${con.legalEntityDetails.registeredLegalName}]` : ''}
                  </p>
                )}
                {con.linkedDealIds && (
                  <p className="text-[10px] text-slate-500 mt-0.5">Linked Deals: {con.linkedDealIds}</p>
                )}
              </div>
              <Can permission="users:edit">
                <button onClick={() => setEditingContact(con)} className="p-2 bg-slate-950 hover:bg-slate-800 text-slate-400 hover:text-white rounded-xl border border-slate-800 transition-colors cursor-pointer"><Edit2 size={14} /></button>
              </Can>
            </div>
          ))}

          {((activeTab === 'companies' && filterBySearch(companies, []).length === 0) ||
            (activeTab === 'legalEntities' && filterBySearch(legalEntities, []).length === 0) ||
            (activeTab === 'contacts' && filterBySearch(contacts, []).length === 0)) && !errorMessage && (
            <div className="py-12 text-center border border-dashed border-slate-800 rounded-2xl text-slate-500 text-sm">
              {searchTerm.trim()
                ? 'No records match your search. Try a different term.'
                : 'No active records found matching this pipeline layout screen state.'}
            </div>
          )}

          <div ref={sentinelRef} className="h-1" />
        </div>
      )}

      {editingCompany && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-md w-full space-y-4 shadow-2xl">
            <h3 className="text-base font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-2 flex items-center gap-1.5">
              <Building2 size={16} className="text-blue-500" /> Modify Corporate Company Profile
            </h3>
            <form onSubmit={handleUpdateCompanySubmit} className="space-y-3">
              <div>
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Business Operating Name</label>
                <input
                  type="text" required value={editingCompany.businessName || ''}
                  onChange={(e) => setEditingCompany({ ...editingCompany, businessName: e.target.value })}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Registration No</label>
                  <input
                    type="text" value={editingCompany.registrationNumber || ''}
                    onChange={(e) => setEditingCompany({ ...editingCompany, registrationNumber: e.target.value })}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">VAT Number</label>
                  <input
                    type="text" value={editingCompany.vatNumber || ''}
                    onChange={(e) => setEditingCompany({ ...editingCompany, vatNumber: e.target.value })}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>
              </div>
              <div>
                <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Head Office Address Location</label>
                <input
                  type="text" value={editingCompany.headOfficeAddress || ''}
                  onChange={(e) => setEditingCompany({ ...editingCompany, headOfficeAddress: e.target.value })}
                  className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Credit Limit Boundary</label>
                  <input
                    type="number" value={editingCompany.corporateCreditLimit || 0}
                    onChange={(e) => setEditingCompany({ ...editingCompany, corporateCreditLimit: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>
                <div>
                  <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-1">Industry Vertical</label>
                  <input
                    type="text" value={editingCompany.industryVertical || ''}
                    onChange={(e) => setEditingCompany({ ...editingCompany, industryVertical: e.target.value })}
                    className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>
              </div>
              <div className="flex gap-3 pt-3">
                <button type="button" onClick={() => setEditingCompany(null)} className="flex-1 py-2 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 cursor-pointer">Cancel</button>
                <Can permission="agencies:edit">
                  <button type="submit" className="flex-1 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-xl cursor-pointer">Save Changes</button>
                </Can>
              </div>
            </form>
          </div>
        </div>
      )}

      {(editingContact || isContactFormOpen) && (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md flex items-center justify-center p-4 z-50 animate-fade-in">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 max-w-lg w-full space-y-4 shadow-2xl">
            <h3 className="text-base font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-2 flex items-center gap-1.5">
              <User size={16} className="text-blue-500" /> {editingContact ? 'Edit Contact Profile' : 'Add New Contact'}
            </h3>
            <form onSubmit={editingContact ? handleUpdateContactSubmit : handleCreateContact} className="space-y-3">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Full Name</label><input required type="text" value={editingContact ? editingContact.fullName : contactForm.fullName} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, fullName: e.target.value }) : setContactForm({ ...contactForm, fullName: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Email</label><input required type="email" value={editingContact ? editingContact.emailAddress : contactForm.emailAddress} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, emailAddress: e.target.value }) : setContactForm({ ...contactForm, emailAddress: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Phone</label><input type="text" value={editingContact ? editingContact.phoneNumber : contactForm.phoneNumber} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, phoneNumber: e.target.value }) : setContactForm({ ...contactForm, phoneNumber: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Job Title</label><input type="text" value={editingContact ? editingContact.positionJobTitle : contactForm.positionJobTitle} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, positionJobTitle: e.target.value }) : setContactForm({ ...contactForm, positionJobTitle: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Role Type</label><select value={editingContact ? editingContact.contactRoleType : contactForm.contactRoleType} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, contactRoleType: e.target.value }) : setContactForm({ ...contactForm, contactRoleType: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"><option>Buyer</option><option>Seller</option><option>Broker</option></select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Preferred Area</label><input type="text" placeholder="e.g. Sandton" value={editingContact ? (editingContact.area || '') : contactForm.area} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, area: e.target.value }) : setContactForm({ ...contactForm, area: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Budget</label><input type="text" placeholder="e.g. R 1.5M - 2M" value={editingContact ? (editingContact.budget || '') : contactForm.budget} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, budget: e.target.value }) : setContactForm({ ...contactForm, budget: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" /></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Company</label><select value={editingContact ? editingContact.associatedCompanyId || '' : contactForm.associatedCompanyId} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, associatedCompanyId: e.target.value }) : setContactForm({ ...contactForm, associatedCompanyId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"><option value="">None</option>{companies.map((c) => <option key={c.companyId} value={c.companyId}>{c.businessName}</option>)}</select></div>
                <div className="space-y-1"><label className="text-[10px] text-slate-400 uppercase">Legal Entity</label><select value={editingContact ? editingContact.associatedLegalEntityId || '' : contactForm.associatedLegalEntityId} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, associatedLegalEntityId: e.target.value }) : setContactForm({ ...contactForm, associatedLegalEntityId: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500 cursor-pointer"><option value="">None</option>{legalEntities.map((l) => <option key={l.legalEntityId} value={l.legalEntityId}>{l.registeredLegalName}</option>)}</select></div>
                <div className="space-y-1 md:col-span-2">
                  <label className="text-[10px] text-slate-400 uppercase">Linked Deal IDs (CSV)</label>
                  <input type="text" placeholder="e.g. 12,15" value={editingContact ? editingContact.linkedDealIds || '' : contactForm.linkedDealIds} onChange={(e) => editingContact ? setEditingContact({ ...editingContact, linkedDealIds: e.target.value }) : setContactForm({ ...contactForm, linkedDealIds: e.target.value })} className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500" />
                  <p className="text-[10px] text-slate-500">Comma-separated deal IDs to associate this contact with existing deals.</p>
                </div>
              </div>
              <div className="flex gap-3 pt-2">
                <button type="button" onClick={() => { editingContact ? setEditingContact(null) : setIsContactFormOpen(false); }} className="flex-1 py-2.5 bg-slate-950 hover:bg-slate-800 text-slate-400 text-xs font-bold rounded-xl border border-slate-800 transition-colors cursor-pointer">Cancel</button>
                <Can permission={editingContact ? 'users:edit' : 'users:create'}>
                  <button type="submit" className="flex-1 py-2.5 bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold rounded-xl shadow-md transition-colors cursor-pointer">{editingContact ? 'Save Changes' : 'Create Contact'}</button>
                </Can>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}